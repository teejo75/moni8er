Moni8er
==========================

Moni8er is a utility for creating the necessary database files that the Mede8er device requires for its "Jukebox" view on X2 and newer models.

If you're not using jukebox view, you do not need this utility.

Moni8er is a program that can scan your Mede8er Jukebox folders and create the necessary Mede8er.db files so that you no longer need to use
the "Scan Folder" function within the Mede8er interface.

The utility checks if a database already exists, and updates only changed data, or if no database exists, creates it from scratch.

This program has been written from scratch by reverse engineering the Mede8er.db database file, and by using information found on the Mede8er.com forums.

See SOURCE.txt for more information.

!! IMPORTANT !!
============================
It is imperative that you ensure your movie folder structure is in a clean state, otherwise you WILL have problems, not only with this utility, 
but with the mede8er device itself.

  * Each movie file must be in its own folder and must have ONE valid XML file for it. Having more than one valid XML file will result in duplicate entries in the database.
    If you have some kind of additional metadata xml file in your folders for some other software, add the common filename to the ignorexmlfiles file. This only applies to
    files with the extension '.xml', as those are the only files that Moni8er cares about.
  * If you have a movie file that is cut in to multiple separate parts, make sure that you only have a single XML file.
  * Any extra movie files must be moved to a separate folder (Like extras etc).
  * Create an empty file called "mjbignore.xml" or ".mjbignore" in any folders that you want the scan to ignore (provided that they have valid XML files that you want ignored).

Installation
============
No installation required. Extract the entire archive into its own folder, and run Moni8er.exe. That's it.
To remove it, delete the files.

Usage
=====

Click the 'Add' button to browse for and select your Jukebox root folders. This is the folder for which your jukebox view will appear, and also where the Mede8er.db will reside.

Entries with a check box will be processed, and a database will be created for each folder. This allows you to process individual folders without removing/re-adding folders.
Click the 'Remove' button to remove /highlighted/ items from the list.
The checkbox next to an entry determines if that folder will be processed when you click the Update button.
Click the 'Update' button to scan the Jukebox folders and create or update the Mede8er.db files. This process may take several minutes.

Once you are done, you can close the application. Your folder selections will be remembered automatically.

For information on Jukebox folders, please see this document: http://www.mede8er.com/mede8er_y2m_user_guide_V2.05a.html

An example Jukebox folder structure:
See local file ![Moni8er/Jukebox-Example.png]

In the case of the example above, your Jukebox folders would be 

  * X:\Kids Movies
  * X:\Movies
  * X:\Old Movies

  Alternatively, instead of X:\Movies you could have:

  * X:\Movies\PG
  * X:\Movies\PG13
  * X:\Movies\R

File Listing
============
ignorexmlfiles - Add xml files that you want Moni8er to ignore during processing.
Jukebox-Example.png
Moni8er.exe - Main program file. Requires .NET Framework 4.5. Download: http://www.microsoft.com/en-us/download/details.aspx?id=30653
Moni8er.dat - Defunct.
Moni8er.Settings - Defunct.
Moni8er.log - Created after first run. Logs various run time information. Can be switched off with the check box.
Moni8er.Settings.dll - Support library // Defunct. No longer used or shipped.
Moni8er.Database.dll - Support library
Moni8er.Logging.dll - Support Library
Moni8er.Process.dll - Support Library
Moni8er.Update.dll - Support Library
System.Data.SQLite.dll - SQLite Support - for creating the Mede8er.db files.
x86\SQLite.Interop.dll - SQLite Support (32bit)
x64\SQLite.Interop.dll - SQLite Support (64bit)
Update.dll - Update notifier // Defunct. No longer used or shipped.
README.txt - This file.
CHANGELOG.TXT - Details changes to Moni8er.

Credits
=======
http://www.mede8erforum.com/index.php/topic,8099.0.html - The thread that started this
ProgramLogger by Tom Schrieber: https://code.google.com/p/programlogger/
http://www.digitalcoding.com/
Various articles on stackoverflow.com
Various articles on msdn.microsoft.com
Many (many) searches via google.
