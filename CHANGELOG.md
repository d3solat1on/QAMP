# CHANGELOG
## (12.06.2026) Version 1.7.5:
    - Minor optimizations have been made to the application.  
    - Fixed a bug with adding files to a playlist.
    - The left "Playlists" column is now also compact if compact mode is selected in the settings.  
    - Removed old files from the project.

## (07.06.2026) Version 1.7.4:
    - Added English language support (The Favorites playlist is created with a Russian title and description, this will be fixed later xD).  
    - Support for more audio formats has also been added.  

## (02.06.2026) Version 1.7.3:
    - A new window for sound settings has appeared.  
    - The equalizer has been redesigned.  
    - Various audio parameters have been added, such as: Reverb, Echo, Vocal Boost, Tempo and Pitch Shift, and more.  
    - A new column has been added to the track database: BPM.
    - A new button has been added to the "Track Details" window in the track tag editing mode: find out the track's BPM.  
    - Added the ability to add your own themes to the application, in addition to dark and light.  
    - Native audio processing core (`QampCore.dll`): All mathematical calculation logic, logarithmic frequency distribution, and FFT decibel normalization have been completely ported to C++. This allows for maximum audio stream processing speed and zero memory allocation in the managed heap (Garbage Collector).  
    - Gravity physics: Native calculation of spectral band fall inertia and free-fall peak point physics (with a delay at the top) have been implemented in C++.  
    - Rendering Optimization: The `ScottPlot 5` component has been switched to non-interactive mode (`NonInteractive`) with the Y (0.0 - 1.0) and X axes rigidly fixed. The graphics engine now exclusively displays ready-made data arrays on the screen, without wasting resources on unnecessary calculations and processing mouse events. The Spectre consistently delivers a respectable 180 FPS on high-Hz monitors. (I've personally verified this.)  
    - Added MemoryOptimizer class to remove cache from RAM after closing heavy windows.  


## (01.06.2026) Version 1.7.2:
    - New notification system (Toast Notifications): Notifications are divided into two types. New lightweight notifications have been added for successful routine actions (saving tags, adding a track to favorites, creating a playlist). They smoothly slide in from the bottom of the screen, change statuses in real time, and don't require clicking buttons. Modal windows with an "OK" button now appear only when critical errors occur, so the text can be easily read or copied.  
    - Library panel redesign: The "LIBRARY" label in the left column is now neatly centered. All playlist control buttons are now arranged in a single symmetrical row and automatically adjust to the full panel width.  
    - New feature: A "Sort playlists" button has been added to the library control panel for quickly organizing playlists.  
    - Asynchronous data loading: The processes of reading metadata and loading information in the "Playlist Details" window have been switched to asynchronous. Heavy disk operations no longer block or freeze the main player interface.  
    - Dynamic Art Placeholders: When a playlist lacks custom art, the ShowInfoPlaylist window now automatically renders a neat vector placeholder geometry instead of the empty space. This is the exact same logic as the "Edit Playlist" window.  
    - Removed old png files from the project.

## (28.05.2026) Version: 1.7.1:
    - A "Open Spectrum in Full Screen" button has been added to the bottom control panel.  
    - BASS registration data has been moved to a separate file. More details in readme.md.   
    - Placeholders are now created instead of cover art for playlists that don't have cover art. Placeholders are updated with the app's primary color.  
    - Methods responsible for track cover art have been removed from MediaControlsManager. They weren't working anyway.  

## (26.05.2026) Version: 1.7.0:  
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

## (22.05.2026) Version: 1.6.8:  
    - The audio engine has changed from NAudio to Bass.net. Unlike NAudio, which ran within a managed .NET environment, BASS is compiled in pure C/Assembly. Audio decoding now occurs natively, with virtually zero latency and minimal CPU load.  
    - This has improved the performance of the spectrogram.  
    - The equalizer is currently not working correctly.  

## (22.05.2026) Version: 1.6.7:  
    - Now the right panel is empty if there is no track, like the bottom left part (for example, if the database is empty).  
    - The "Detailed Playlist Information" window now shows more data (track ID, as well as the time it was added to the playlist, down to the second).  
    - The code has been optimized, resulting in the garbage collector running less frequently, reducing CPU and memory load.  

## (20.05.2026) Version: 1.6.6:  
    - Changed track tags are now saved correctly in the database.  
    - Accordingly, the central DataGrid is also updated correctly.  
    - Now all buttons are made in a uniform style (I hope).  
    - The Detailed Track Information window now correctly returns focus after closing.  
    - Improved Full-screen spectrum mode.  
    - Minor bugs have been fixed.  

## (18.05.2026) Version: 1.6.5:  
    - Added a "Detailed Playlist Information" window.  
    - Buttons have been improved.  
    - Full-screen spectrum mode has been added (Beta). Opens and closes with the keyboard shortcut ctrl + w.  
    - The GOST_TYPE_A font has been added to the program resources (for those who don't have the font installed on their system).  
    - The mini window has been fixed so that it does not steal focus from the active window.  

## (13.05.2026) Version: 1.6.4:  
    - Now all buttons are made in a single style (wow......).  

## (26.04.2026) Version: 1.6.3:  
    - The cover of the "Favorites" playlist now adapts to the main color of the app.  
    - Fixed some UI bugs.  
    - Added automatic startup function with the system.  

## (20.04.2026) Version: 1.6.2:  
    - Fixed a bug with updating colors in the spectrum.  
    - Fixed a bug with updating icons in Control Panel.  
    - The statistics window is now updated without re-opening the player.  

## (19.04.2026) Version: 1.6.1:  
    - The logic for adding a folder to a playlist has been fixed (probably, maybe not).  
    - Added information about memory consumption to the Settings window.  
    - Now settings.json, app_info.log, and crash_log.txt are stored in the same folder as library.db. This is done for portability, as well as to solve the issue of write blocking when installing the player in system folders (e.g., Program Files) and ensures stable operation of settings autosaving without administrator rights.  
    - Fixed Lyrics Mode and added hot keys (Home, End, PageUp, PageDown).  

## (18.04.2026) Version: 1.6.0:  
    - The "Statistics" window has been improved. It now displays the most-listened-to artist and the top 10 unlistened tracks.  