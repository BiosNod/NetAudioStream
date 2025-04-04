1. Open .sln using VisualStudio
2. Go to Project -> NuGet packages -> Search and install: NAudio (2.2.1), System.Buffers (4.6.1)
3. Or go to Tools -> NuGet Package Manager -> Package manager console (Terminal) and run:
   Install-Package NAudio
   Install-Package System.Buffers
4. Build
5. Go to bin folder and run AudioStream.exe, choose server/client mode, choose playback/recording/process audio device, if you use Server - you need to forward port using router to your PC.
6. Another user should run same app and both users can be connected