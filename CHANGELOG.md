# CHANGELOG

(26.05.2026)V: 1.7.0:  
    - Architectural transition: The NAudio library and associated legacy components have been completely removed. The audio engine has been completely migrated to the stable and high-performance low-level BASS.NET platform.  
    - Dependency Cleanup: The project has been completely cleaned of unused and duplicated code from old audio modules.  
    - System Media Transport Controls (SMTC): Support for the Windows 11 system audio control overlay has been successfully implemented.  
    - Integration via background audio session: Bypass operating system limitations for unpackaged Win32 applications using the background model of MediaPlayer. The Windows notification shade now instantly recognizes the player.  
    - Metadata synchronization: Added transfer of track title, artist name, and album name, as well as dynamic updating of the window title.  
    - Media key support: Playback controls (Play, Pause, Next, Previous) now work fully with the keyboard, headphones, and Windows system tray buttons.  
    - Style Unification: A unified visual style has been created and implemented for the DataGrid components and all control buttons outside the bottom panel.  
    - System Tray Update: The tray menu has been completely rewritten and modernized using the H.NotifyIcon.Wpf library, ensuring stable behavior and native responsiveness.  
    - Assembly Metadata: Fixed an issue where the player would appear as "Unknown Application" in the system — AssemblyInfo metadata and the executable file icon are now tightly synchronized with the OS.
    - Change of target platform: The project has been updated to the modern .NET 10.0-windows with an explicit indication of the target SDK version (10.0.19041.0).  
    - Memory optimization (Garbage Collection): Implemented fine-tuning of GC for the desktop player (<ServerGarbageCollection>false</ServerGarbageCollection> and <GarbageCollectionAdaptation>1</GarbageCollectionAdaptation>), reducing RAM consumption in the background.  
    - Fast startup (Ahead-Of-Time): Enabled the <PublishReadyToRun>true</PublishReadyToRun> flag to pre-compile the application into source code, which significantly accelerated the cold start of the player.  

(22.05.2026)V: 1.6.8:  
    - The audio engine has changed from NAudio to Bass.net. Unlike NAudio, which ran within a managed .NET environment, BASS is compiled in pure C/Assembly. Audio decoding now occurs natively, with virtually zero latency and minimal CPU load.  
    - This has improved the performance of the spectrogram.  
    - The equalizer is currently not working correctly.  

(22.05.2026)V: 1.6.7:  
    - Now the right panel is empty if there is no track, like the bottom left part (for example, if the database is empty).  
    - The "Detailed Playlist Information" window now shows more data (track ID, as well as the time it was added to the playlist, down to the second).  
    - The code has been optimized, resulting in the garbage collector running less frequently, reducing CPU and memory load.  

(20.05.2026)V: 1.6.6:  
    - Changed track tags are now saved correctly in the database.  
    - Accordingly, the central DataGrid is also updated correctly.  
    - Now all buttons are made in a uniform style (I hope).  
    - The Detailed Track Information window now correctly returns focus after closing.  
    - Improved Full-screen spectrum mode.  
    - Minor bugs have been fixed.  

(18.05.2026)V: 1.6.5:  
    - Added a "Detailed Playlist Information" window.  
    - Buttons have been improved.  
    - Full-screen spectrum mode has been added (Beta). Opens and closes with the keyboard shortcut ctrl + w.  
    - The GOST_TYPE_A font has been added to the program resources (for those who don't have the font installed on their system).  
    - The mini window has been fixed so that it does not steal focus from the active window.  

(13.05.2026)V: 1.6.4:  
    - Now all buttons are made in a single style (wow......).  

(26.04.2026)V: 1.6.3:  
    - The cover of the "Favorites" playlist now adapts to the main color of the app.  
    - Fixed some UI bugs.  
    - Added automatic startup function with the system.  

(20.04.2026)V: 1.6.2:  
    - Fixed a bug with updating colors in the spectrum.  
    - Fixed a bug with updating icons in Control Panel.  
    - The statistics window is now updated without re-opening the player.  

(19.04.2026)V: 1.6.1:  
    - The logic for adding a folder to a playlist has been fixed (probably, maybe not).  
    - Added information about memory consumption to the Settings window.  
    - Now settings.json, app_info.log, and crash_log.txt are stored in the same folder as library.db. This is done for portability, as well as to solve the issue of write blocking when installing the player in system folders (e.g., Program Files) and ensures stable operation of settings autosaving without administrator rights.  
    - Fixed Lyrics Mode and added hot keys (Home, End, PageUp, PageDown).  

(18.04.2026)V: 1.6.0:  
    - The "Statistics" window has been improved. It now displays the most-listened-to artist and the top 10 unlistened tracks.  