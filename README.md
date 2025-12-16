# MetaverseCloudEngine Unity SDK
Powered by Reach Cloud Ⓒ 2025
### The Metaverse Cloud Engine Unity SDK
This package enables you to install the Metaverse Cloud Engine SDK and perform automatic updates within Unity Editor.

### Requirements
* Unity 2022 LTS
* Install of git on your system: https://git-scm.com/downloads
* Latest version of .NET: https://dotnet.microsoft.com/en-us/download

![image](https://user-images.githubusercontent.com/14853489/188254018-453aae49-a6a3-4e6e-8fd2-fe4bbf6310d1.png)

# Changelog

## 2.205.0
- feat: Add API client logging and session management utilities
- 
- - Implemented API client logging functionality in MetaverseProgram.cs, allowing for detailed HTTP request/response logging.
- - Introduced methods in MetaverseEditorToolsMenu.cs to enable and disable API client logging, and to dump session information for debugging purposes.
- - Enhanced the UploadBundles method in AssetEditor.cs to utilize coroutines for better upload management and error handling.
- - Created AccountTokenUtility.cs for managing access token expiration and JWT parsing, improving session validation processes.
- - Updated MetaverseWebClient.cs to log HTTP requests and responses when API client logging is enabled.

Updated on December 16, 2025
