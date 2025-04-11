

## NetAudioStream - Audio Streaming Solution

### 🔧 Prerequisites
- Visual Studio 2022 (17.0+)
- .NET 7 SDK
- Windows 10/11 (64-bit)

### 🚀 Step-by-Step Setup
1. Open .sln using VisualStudio
2. Go to Project -> NuGet packages -> Search and install: NAudio (2.2.1), System.Buffers (4.6.1)
3. Or go to Tools -> NuGet Package Manager -> Package manager console (Terminal) and run:
   Install-Package NAudio
   Install-Package System.Buffers
4. Build
5. Go to bin folder and run AudioStream.exe, choose server/client mode, choose playback/recording/process audio device, if you use Server - you need to forward port using router to your PC.
6. Another user should run same app and both users can be connected
7. Don't forget to forward server ports at your router or use UPNP option in Network settings in program menu

### 🌟 If you want to capture sound only from the specific process, you also need:
1. git clone https://github.com/BiosNod/ApplicationLoopback.git
2. Open .sln using VisualStudio
3. Build
4. Copy ApplicationLoopback.exe from the build folder the current build folder nearby current NetAudioStream.exe

## 🔧 Network Configuration

### Port Forwarding
- Automatic (UPnP):
  Enable in Settings → Network → UPnP
- Manual:
  TCP: 49800

## 🎯 Usage Modes

### Server Mode
1. Launch → Server
2. Select Source:
   [1] System Audio
   [2] Microphone
   [3] Process Audio

### Client Mode
1. Launch → Client
2. Enter Server IP:Port
3. Select Output Device

## 📁 Technical Specs
| Parameter       | Value               |
|-----------------|---------------------|
| Latency         | 10-1000 ms          |
| Bit Depth       | 16/24/32-bit        |
| Encryption      | AES-256             |