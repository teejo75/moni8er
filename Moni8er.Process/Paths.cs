using Moni8er.Database;
using Moni8er.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;

namespace Moni8er.Process
{
    public delegate void NotifyError(string ErrorText, bool Aborted = false);

    public delegate void NotifyStatus(int Progress, int ProgressMax, string NotifyText);

    public class Paths
    {
        private string _IgnoreFile = System.IO.Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]) + @"\ignorexmlfiles";
        private List<string> _IgnoreList;
        private string _JukeboxPathPlus = "";

        public event NotifyError NotifyErrorEvent;

        public event NotifyStatus NotifyStatusEvent;

        public bool AbortProcess { get; set; }

        /// <summary>
        /// Begins the process to create or update the Mede8er.db file.
        /// </summary>
        /// <param name="JukeboxPath">The path to the folder to scan for valid files</param>
        public void CreateUpdateDatabase(string JukeboxPath)
        {
            if (!JukeboxPath.EndsWith(@"\"))
            {
                _JukeboxPathPlus = JukeboxPath + @"\";
            }
            else
            {
                _JukeboxPathPlus = JukeboxPath;
            }

            if (File.Exists(_IgnoreFile))
            {
                _IgnoreList = populateIgnoreList(_IgnoreFile);
            }
            else
            {
                _IgnoreList = new List<string>();
                _IgnoreList.Add("view.xml");
                _IgnoreList.Add("movieinfo.xml");
            }

            #region Scan Files

            // Scan folders for xml
            if (Log.Logging) { Log.log.Log("Searching for XML files in path " + JukeboxPath, LogFile.LogLevel.Info); }

            NotifyStatusEvent(0, 0, "Scanning " + JukeboxPath);

            List<string> allFiles = new List<string>();
            try
            {
                // Scan through each directory in the Jukebox Patch
                foreach (var directory in Directory.EnumerateDirectories(JukeboxPath, "*", SearchOption.AllDirectories))
                {
                    if (File.Exists(directory + @"\mjbignore.xml") || File.Exists(directory + @"\.mjbignore"))
                    {
                        if (Log.Logging) { Log.log.Log("Found mjbignore, skipping folder " + directory, LogFile.LogLevel.Info); }
                        continue;
                    }
                    List<string> directoryFiles = new List<string>();
                    // Scan through each file in each found directory
                    foreach (var file in Directory.EnumerateFiles(directory))
                    {
                        // If it's an xml and NOT in the ignore list, then add it to a list.
                        if (file.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase) && !_IgnoreList.Contains(Path.GetFileName(file).ToLower()))
                        {
                            if (Log.Logging) { Log.log.Log("Checking " + file, LogFile.LogLevel.Info); }
                            directoryFiles.Add(file);
                        }
                    }
                    if (directoryFiles.Count > 0)
                    {
                        allFiles.AddRange(directoryFiles);
                    }
                }
            }
            catch (UnauthorizedAccessException UAEx)
            {
                if (Log.Logging) { Log.log.Log(UAEx.Message, LogFile.LogLevel.Error); }
                NotifyErrorEvent("An error occurred: " + UAEx.Message);
                return;
            }
            catch (PathTooLongException PathEx)
            {
                if (Log.Logging) { Log.log.Log(PathEx.Message, LogFile.LogLevel.Error); }
                NotifyErrorEvent("An error occurred: " + PathEx.Message);
                return;
            }
            catch (DirectoryNotFoundException DirNFEx)
            {
                if (Log.Logging) { Log.log.Log(DirNFEx.Message, LogFile.LogLevel.Error); }
                NotifyErrorEvent("An error occurred: " + DirNFEx.Message);
                return;
            }

            #endregion Scan Files

            // Database processing starts here.
            bool IsNewDataBase = false;

            if (!File.Exists(JukeboxPath + @"\Mede8er.db"))
            {
                IsNewDataBase = true;
            }

            SQLiteDatabase db = new SQLiteDatabase(JukeboxPath + @"\Mede8er.db");
            string SQL = @"CREATE TABLE IF NOT EXISTS MovieData (HashCode TEXT,Genre TEXT,Year TEXT,Rating TEXT,Folder TEXT, Timestamp TEXT);";
            db.ExecuteNonQuery(SQL);

            #region New Database

            if (IsNewDataBase)
            {
                if (Log.Logging) { Log.log.Log("New database required, processing " + allFiles.Count.ToString() + " files", LogFile.LogLevel.Info); }
                int count = 0;
                NotifyStatusEvent(0, 0, "Creating new database...");
                foreach (string xmlfile in allFiles)
                {
                    if (AbortProcess)
                    {
                        NotifyErrorEvent("Processing Cancelled", true);
                        if (Log.Logging) { Log.log.Log("Processing Cancelled", LogFile.LogLevel.Error); }
                        return;
                    };

                    string[] value = new string[6];
                    value = populateRow(xmlfile, _JukeboxPathPlus);
                    count++;
                    NotifyStatusEvent(count, allFiles.Count, "");
                    if (value[0] != "ERROR")
                    {
                        Dictionary<String, String> InsertData = new Dictionary<String, String>();
                        InsertData.Add("HashCode", value[0]);
                        InsertData.Add("Genre", value[1].Replace("'", "''"));
                        InsertData.Add("Year", value[2]);
                        InsertData.Add("Rating", value[3]);
                        InsertData.Add("Folder", value[4]);
                        InsertData.Add("Timestamp", value[5]);
                        try
                        {
                            db.Insert("MovieData", InsertData);
                        }
                        catch (Exception e)
                        {
                            if (Log.Logging) { Log.log.Log("Error updating database in " + JukeboxPath + "\r\n" + e.Message, LogFile.LogLevel.Error); }
                            NotifyErrorEvent("Error while updating database in " + JukeboxPath + "\r\n" + e.Message);
                            break; // No point in continuing trying to add to a broken database
                        }
                    }
                    else
                    {
                        if (AbortProcess)
                        {
                            NotifyErrorEvent("Processing Cancelled", true);
                            if (Log.Logging) { Log.log.Log("Processing Cancelled", LogFile.LogLevel.Error); }
                            return;
                        };
                        if (Log.Logging) { Log.log.Log("Error while processing " + xmlfile + ": " + value[1], LogFile.LogLevel.Error); }
                        NotifyErrorEvent("Error while processing " + xmlfile);
                    }
                }
            }

            #endregion New Database

            #region Update Database

            else
            {
                // The database already exists, so we must rather update it.

                // First we need to find the entries that we have to remove from the database
                // Compile list of folder names from our file list
                if (Log.Logging) { Log.log.Log("Existing database found, checking " + allFiles.Count.ToString() + " files for changes", LogFile.LogLevel.Info); }
                NotifyStatusEvent(0, 0, "Updating database...");

                Dictionary<string, string> XMLFolderList = new Dictionary<string, string>();
                int ucount = 0;
                foreach (string xmlfile in allFiles)
                {
                    FileInfo fi = new FileInfo(xmlfile);
                    XMLFolderList.Add(Path.GetDirectoryName(xmlfile).Remove(0, _JukeboxPathPlus.Length).Replace("'", "''").Replace(@"\", "/"), DateTimeToUnixTimestamp(fi.LastWriteTime).ToString());
                    ucount++;
                    NotifyStatusEvent(ucount, allFiles.Count, "");
                }

                // Grab comparitive data from the database and put it in a list.
                Dictionary<string, string> DBFolderList = new Dictionary<string, string>();
                SQL = @"SELECT Folder, Timestamp from MovieData;";
                System.Data.DataTable ComparitiveDataTable = db.GetDataTable(SQL);
                foreach (System.Data.DataRow row in ComparitiveDataTable.Rows)
                {
                    DBFolderList.Add(row[0].ToString().Replace("'", "''"), row[1].ToString());
                }

                #region Removals

                // Use LINQ to check the difference between the two lists, and dump the exceptions to RemoveList.
                List<string> RemoveList = DBFolderList.Keys.Except(XMLFolderList.Keys).ToList();
                if (RemoveList.Count > 0)
                {
                    if (Log.Logging) { Log.log.Log("Found " + RemoveList.Count.ToString() + " removed folders", LogFile.LogLevel.Info); }
                    // There are entries to remove from the database
                    int Deletions = 0;
                    foreach (string row in RemoveList)
                    {
                        if (Log.Logging) { Log.log.Log("Folder no longer exists in filesystem: " + row, LogFile.LogLevel.Info); }
                        SQL = @"DELETE FROM MovieData WHERE Folder = '" + row + "';";
                        int result = db.ExecuteNonQuery(SQL);
                        if (result > 1)
                        {
                            if (Log.Logging) { Log.log.Log("More than one record for " + row + " was removed from " + JukeboxPath + " database. Database could be damaged. Delete it and allow Moni8er to rebuild it.", LogFile.LogLevel.Warn); }
                        }
                        Deletions += result;
                        DBFolderList.Remove(row); // Entry should be removed from DB List
                        NotifyStatusEvent(Deletions, RemoveList.Count, "");
                    }
                    if (Log.Logging)
                    {
                        var msg = "";
                        if (Deletions == 1)
                        {
                            msg = "Removed " + Deletions + " entry from the database.";
                        }
                        else
                        {
                            msg = "Removed " + Deletions + " entries from database";
                        };
                        Log.log.Log(msg, LogFile.LogLevel.Info);
                    }
                }
                else if (Log.Logging) { Log.log.Log("Did not find any folders that have been removed from the filesystem.", LogFile.LogLevel.Info); }

                #endregion Removals

                #region Changed

                // Now we must find changed entries.
                // Use LINQ to check the difference between the two lists, and dump the exceptions to RemoveList.

                // Process changed files now
                var ChangedListKeys = DBFolderList.Where(entry => XMLFolderList[entry.Key] != entry.Value).ToList();
                if (ChangedListKeys.Count > 0)
                {
                    List<string[]> ChangedList = new List<string[]>();
                    foreach (var entry in ChangedListKeys)
                    {
                        string[] value = new string[2];
                        var result = allFiles.First(x => x.Contains(entry.Key.Replace("/", @"\").Replace("''", "'")));
                        value[0] = entry.Key;
                        value[1] = result;
                        ChangedList.Add(value);
                    }
                    if (Log.Logging) { Log.log.Log("Found " + ChangedList.Count.ToString() + " changed files to update records for.", LogFile.LogLevel.Info); }

                    int ccount = 0;
                    foreach (string[] entry in ChangedList)
                    {
                        string[] value = new string[6];
                        value = populateRow(entry[1], _JukeboxPathPlus);
                        ccount++;
                        NotifyStatusEvent(ccount, ChangedList.Count, "");
                        if (value[0] != "ERROR")
                        {
                            Dictionary<String, String> InsertData = new Dictionary<String, String>();
                            InsertData.Add("HashCode", value[0]);
                            InsertData.Add("Genre", value[1].Replace("'", "''"));
                            InsertData.Add("Year", value[2]);
                            InsertData.Add("Rating", value[3]);
                            InsertData.Add("Folder", value[4]);
                            InsertData.Add("Timestamp", value[5]);
                            try
                            {
                                db.Update("MovieData", InsertData, "Folder = '" + entry[0] + "'");
                            }
                            catch (Exception e)
                            {
                                if (Log.Logging) { Log.log.Log("Error updating database in " + JukeboxPath + "\r\n" + e.Message, LogFile.LogLevel.Error); }
                                NotifyErrorEvent("Error updating database in " + JukeboxPath + "\r\n" + e.Message);
                                break; // No point in continuing trying to add to a broken database
                            }
                        }
                        else
                        {
                            if (AbortProcess)
                            {
                                NotifyErrorEvent("Processing Cancelled", true);
                                if (Log.Logging) { Log.log.Log("Processing Cancelled", LogFile.LogLevel.Error); }
                                return;
                            };
                            if (Log.Logging) { Log.log.Log("Error while processing " + entry[1] + ": " + value[1], LogFile.LogLevel.Error); }
                            NotifyErrorEvent("Error while processing " + entry[1] + ": " + value[1]);
                        }
                    }
                }
                else if (Log.Logging) { Log.log.Log("No changed files found.", LogFile.LogLevel.Info); }

                #endregion Changed

                #region New Files

                // Process New files now
                var NewListKeys = XMLFolderList.Keys.Except(DBFolderList.Keys).ToList();
                if (NewListKeys.Count > 0)
                {
                    if (Log.Logging) { Log.log.Log("Found " + NewListKeys.Count.ToString() + " new files to add.", LogFile.LogLevel.Info); }
                    List<string> NewList = new List<string>();
                    foreach (var entry in NewListKeys)
                    {
                        var result = allFiles.First(x => x.Contains(entry.Replace("/", @"\").Replace("''", "'")));
                        NewList.Add(result);
                    }

                    int nkcount = 0;
                    foreach (string entry in NewList)
                    {
                        string[] value = new string[6];
                        value = populateRow(entry, _JukeboxPathPlus);
                        nkcount++;
                        NotifyStatusEvent(nkcount, NewList.Count, "");
                        if (value[0] != "ERROR")
                        {
                            Dictionary<String, String> InsertData = new Dictionary<String, String>();
                            InsertData.Add("HashCode", value[0]);
                            InsertData.Add("Genre", value[1].Replace("'", "''"));
                            InsertData.Add("Year", value[2]);
                            InsertData.Add("Rating", value[3]);
                            InsertData.Add("Folder", value[4]);
                            InsertData.Add("Timestamp", value[5]);
                            try
                            {
                                db.Insert("MovieData", InsertData);
                            }
                            catch (Exception e)
                            {
                                if (Log.Logging) { Log.log.Log("Error updating database " + JukeboxPath + "\r\n" + e.Message, LogFile.LogLevel.Error); }
                                NotifyErrorEvent("Error updating database " + JukeboxPath + "\r\n" + e.Message);
                                break; // No point in continuing trying to add to a broken database
                            }
                        }
                        else
                        {
                            if (AbortProcess)
                            {
                                NotifyErrorEvent("Processing Cancelled", true);
                                if (Log.Logging) { Log.log.Log("Processing Cancelled", LogFile.LogLevel.Error); }
                                return;
                            };
                            if (Log.Logging) { Log.log.Log("Error while processing " + entry + ": " + value[1], LogFile.LogLevel.Error); }
                            NotifyErrorEvent("Error while processing " + entry + ": " + value[1]);
                        }
                    }
                }
                else if (Log.Logging) { Log.log.Log("No new files found.", LogFile.LogLevel.Info); }

                #endregion New Files
            }
        }

        #endregion Update Database

        /// <summary>
        /// Process each movie XML file to calculate the necessary data to populate a single data row in Mede8er.db
        /// </summary>
        /// <param name="xmlfile">Movie Metadata file</param>
        /// <param name="rootPath">Root Jukebox path (Location of Mede8er.db)</param>
        /// <returns>A string array populated with one row of Mede8er data</returns>
        private string[] populateRow(string xmlfile, string rootPath)
        {
            string[] value = new string[6];

            // Do this first so that we error out quicker
            XmlDocument xmlDocument = new XmlDocument();
            try
            {
                xmlDocument.Load(xmlfile);
            }
            catch (XmlException XmlEx)
            {
                value[0] = "ERROR";
                value[1] = XmlEx.Message;
                return value; // exits the routine
            }
            catch (IOException XmlIO)
            {
                value[0] = "ERROR";
                value[1] = XmlIO.Message;
                return value; // exits the routine
            }

            value[4] = Path.GetDirectoryName(xmlfile).Remove(0, rootPath.Length).Replace("'", "''").Replace(@"\", "/"); // Folder
            value[0] = CalculateMD5Hash(value[4]); // HashCode
            FileInfo fi = new FileInfo(xmlfile);
            value[5] = DateTimeToUnixTimestamp(fi.LastWriteTime).ToString(); // Timestamp

            XmlNamespaceManager xmlnm = new XmlNamespaceManager(xmlDocument.NameTable);
            xmlnm.AddNamespace("ns", "http://www.w3.org/2001/XMLSchema");
            // Search for //movie node and abort if it doesn't exist.
            if (xmlDocument.SelectNodes("//movie").Count == 0)
            {
                value[0] = "ERROR";
                value[1] = "Not a valid movie XML file";
                return value; // exits the routine
            }
            if (xmlDocument.SelectNodes("//year").Count == 0)
            {
                value[2] = "0"; // Sanity check ?
            }
            else
            {
                value[2] = xmlDocument.SelectNodes("//year").Item(0).InnerText.Trim(); // Year
            }
            if (value[2] == "") { value[2] = "0"; }
            if (xmlDocument.SelectNodes("//rating").Count == 0) { value[3] = "0"; } // Sanity
            else { value[3] = ConvertRating(xmlDocument.SelectNodes("//rating").Item(0).InnerText.Trim()); } // Rating

            int NameNodeCount = xmlDocument.SelectNodes("//genre/name").Count; // If this has values, then
            int GenreNodeCount = xmlDocument.SelectNodes("//genres/genre").Count; // this shouldn't, and vice versa.

            // No more than 3 genres has been seen in the Mede8er.db
            if (NameNodeCount > 3) { NameNodeCount = 3; }
            if (GenreNodeCount > 3) { GenreNodeCount = 3; }

            if ((NameNodeCount > 0) & (GenreNodeCount > 0)) // A bit of a sanity check?
            {
                value[0] = "ERROR";
                value[1] = "XML File Error Both //genre/name and //genres/genre nodes exist. Check: " + xmlfile;
                return value; // exits the routine
            }
            if ((NameNodeCount == 0) & (GenreNodeCount == 0))
            {
                value[1] = "";
            }
            if (NameNodeCount > 0)
            {
                if (NameNodeCount == 1) { value[1] += xmlDocument.SelectNodes("//genre/name").Item(0).InnerText.Trim(); }
                else
                {
                    for (int i = 0; i <= NameNodeCount - 2; i++)
                    {
                        value[1] += xmlDocument.SelectNodes("//genre/name").Item(i).InnerText.Trim() + "/";
                    }
                    // Add the final value
                    value[1] += xmlDocument.SelectNodes("//genre/name").Item(NameNodeCount - 1).InnerText.Trim();
                }
            }
            if (GenreNodeCount > 0)
            {
                if (GenreNodeCount == 1) { value[1] += xmlDocument.SelectNodes("//genres/genre").Item(0).InnerText.Trim(); }
                else
                {
                    for (int i = 0; i <= GenreNodeCount - 2; i++)
                    {
                        value[1] += xmlDocument.SelectNodes("//genres/genre").Item(i).InnerText.Trim() + "/";
                    }
                    // Add the final value
                    value[1] += xmlDocument.SelectNodes("//genres/genre").Item(GenreNodeCount - 1).InnerText.Trim();
                }
            }
            return value;
        }

        #region Utility Functions

        /// <summary>
        /// Calculate an MD5 hash of a string
        /// </summary>
        /// <param name="input">String value to hash</param>
        /// <returns>Hash value of input string as string</returns>
        ///
        private static string CalculateMD5Hash(string input)
        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            string sb = "";

            for (int i = 0; i < hash.Length; i++)
            {
                sb += hash[i].ToString("x2");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Convert the rating found in Movie Metadata to base 5 format used by Mede8er
        /// </summary>
        /// <param name="input">String value of rating from Metadata</param>
        /// <returns>String value of Mede8er rating</returns>
        private string ConvertRating(string input)
        {
            if (input.IndexOf(".") > 0) { input = input.Replace(".", ""); } // Strip the . out of the IMDB rating
            return Math.Round((Convert.ToDecimal(input) / 10) / 2, MidpointRounding.AwayFromZero).ToString();
        }

        /// <summary>
        /// Convert DateTime to Unix time stamp
        /// </summary>
        /// <param name="_DateTime">DateTime variable to convert</param>
        /// <returns>Return Unix time stamp as long type</returns>
        private long DateTimeToUnixTimestamp(DateTime _DateTime)
        {
            TimeSpan _UnixTimeSpan = (_DateTime - new DateTime(1970, 1, 1, 0, 0, 0));
            return (long)_UnixTimeSpan.TotalSeconds;
        }

        /// <summary>
        /// Populates the ignore list from disk file
        /// </summary>
        /// <param name="file">Full path to filename</param>
        /// <returns>A string list of filenames to ignore</returns>
        private List<string> populateIgnoreList(string file) // #TODO
        {
            List<string> _filelist = new List<string>();
            var lines = File.ReadAllLines(file);
            foreach (var line in lines)
            {
                if (line.StartsWith("#"))
                {
                    continue;
                }
                else
                {
                    if (Path.GetExtension(line).ToLower() == ".xml")
                    {
                        _filelist.Add(Path.GetFileName(line).ToLower());
                    }
                }
            }
            return _filelist;
        }

        #endregion Utility Functions
    }
}