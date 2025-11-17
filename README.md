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

## 2.204.33
- Add DotNow proxy and caching for MetaverseDotNetScript
- 
- - Implemented MetaverseDotNetScriptBaseProxy to enable DotNow script instantiation and method invocation.
- - Created MetaverseDotNetScriptCache to manage DotNow AppDomain and assembly loading for script instances.
- - Introduced MetaverseDotNetScriptILScanner for IL-level scanning to detect usage of blocked CLR APIs.
- - Added MetaverseDotNetScriptSecurity to enforce security policies on assemblies and types.
- - Included metadata files for new scripts to ensure proper integration with Unity.

Updated on November 17, 2025
