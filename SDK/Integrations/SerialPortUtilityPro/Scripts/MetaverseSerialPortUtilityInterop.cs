using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.SPUP
{
    public static class MetaverseSerialPortUtilityInterop
    {
        private static Type _spupType;

        public enum OpenSystem
        {
            NumberOrder = 0,
            Usb = 1,
            Pci = 2,
            BluetoothSsp = 3,
            TcpSerialEmulatorClient = 10,
            TcpSerialEmulatorServer = 11,
        }

        public enum InstanceMethodID
        {
	        IsOpenProcessing,
	        IsOpened,
	        Write,
	        Open,
	        Close,
        }

        public enum StaticMethodID
        {
	        
        }

        public enum SettableFieldID
        {
	        OpenMethod,
	        IsAutoOpen,
        }

        public enum GettableFieldID
        {
	        SystemEventObject,
	        ReadCompleteEventObject,
        }
        
        public enum SettablePropertyID
		{
	        VendorID,
	        Port,
	        ProductID,
	        SerialNumber,
	        DeviceName,
		}

		public enum GettablePropertyID
		{
			SerialNumber,
		}

		public enum SerialPortEventName
		{
			
		}
        
        public class DeviceInfo
        {
            public string Vendor;
            public string Product;
            public string SerialNumber;
            public string PortName;
            public OpenSystem? ParsedOpenSystem;
            
            public override string ToString()
			{
				return $"{Vendor},{Product},{SerialNumber},{PortName}";
			}
            
            public static bool TryParse(string deviceInfoString, out DeviceInfo deviceInfo)
			{
				deviceInfo = new DeviceInfo();
				var deviceInfoParts = deviceInfoString.Split(',');
				if (deviceInfoParts.Length < 4)
					return false;
				deviceInfo.Vendor = deviceInfoParts[0];
				deviceInfo.Product = deviceInfoParts[1];
				deviceInfo.SerialNumber = deviceInfoParts[2];
				deviceInfo.PortName = deviceInfoParts[3];
				if (deviceInfoParts.Length != 5) return true;
				if (Enum.TryParse(deviceInfoParts[4], out OpenSystem openSystem))
					deviceInfo.ParsedOpenSystem = openSystem;
				return true;
			}
        }
        
        private static Type GetSerialPortUtilityProType()
        {
	        return _spupType ??= AppDomain.CurrentDomain.GetAssemblies()
		        .SelectMany(x => x.GetTypes())
		        .FirstOrDefault(x => x.Name == "SerialPortUtilityPro");
        } 
        
        public static void EnsureComponent(ref Component spupComponent, GameObject gameObject)
        {
            if (!spupComponent)
            {
                spupComponent = gameObject.GetComponentsInParent<Component>()
                    .FirstOrDefault(x => x.GetType().Name == "SerialPortUtilityPro");
            }
            else
            {
                if (spupComponent.GetType().Name != "SerialPortUtilityPro")
                {
	                spupComponent = spupComponent.GetComponents<Component>()
		                .FirstOrDefault(x => x.GetType().Name == "SerialPortUtilityPro");
                }
            }
        }
        
        public static void SubscribeToEvent(Component spupComponent, ref EventInfo eventInfo, SerialPortEventName eventName, Delegate handler)
		{
			if (!spupComponent) 
				return;
			
			eventInfo ??= spupComponent
				.GetType()
				.GetEvents()
				.FirstOrDefault(x => x.Name == eventName.ToString());
				
			eventInfo?.AddEventHandler(spupComponent, handler);
		}

		public static void UnsubscribeFromEvent(Component spupComponent, ref EventInfo eventInfo, SerialPortEventName eventName, Delegate handler)
		{
			if (!spupComponent) 
				return;
			
			eventInfo ??= spupComponent
				.GetType()
				.GetEvents()
				.FirstOrDefault(x => x.Name == eventName.ToString());
				
			eventInfo?.RemoveEventHandler(spupComponent, handler);
		}
		
        public static T CallStaticMethod<T>(ref MethodInfo method, StaticMethodID methodName, params object[] parameters)
        {
            method ??= GetSerialPortUtilityProType()
                .GetMethods()
                .FirstOrDefault(x =>
                    x.Name == methodName.ToString() &&
                    x.GetParameters().Length == parameters.Length &&
                    x.GetParameters().Select(y => y.ParameterType).SequenceEqual(parameters.Select(y => y.GetType())));
                
            return (T) method?.Invoke(null, parameters);
        }
        
        public static void CallStaticMethod(ref MethodInfo method, StaticMethodID methodName, params object[] parameters)
        {
            method ??= GetSerialPortUtilityProType()
                .GetMethods()
                .FirstOrDefault(x =>
                    x.Name == methodName.ToString() &&
                    x.GetParameters().Length == parameters.Length &&
                    x.GetParameters().Select(y => y.ParameterType).SequenceEqual(parameters.Select(y => y.GetType())));
                
            method?.Invoke(null, parameters);
        }
        
        public static T CallInstanceMethod<T>(Component spupComponent, ref MethodInfo method, InstanceMethodID methodName, params object[] parameters)
        {
            if (!spupComponent) 
                return default;
            
            method ??= spupComponent
                .GetType()
                .GetMethods()
                .FirstOrDefault(x =>
                    x.Name == methodName.ToString() &&
                    x.GetParameters().Length == parameters.Length &&
                    x.GetParameters().Select(y => y.ParameterType).SequenceEqual(parameters.Select(y => y.GetType())));
                
            return (T) method?.Invoke(spupComponent, parameters);
        }
        
        public static void CallInstanceMethod(Component spupComponent, ref MethodInfo method, InstanceMethodID methodName, params object[] parameters)
        {
            if (!spupComponent) 
                return;
            
            method ??= spupComponent
                .GetType()
                .GetMethods()
                .FirstOrDefault(x =>
                    x.Name == methodName.ToString() &&
                    x.GetParameters().Length == parameters.Length &&
                    x.GetParameters().Select(y => y.ParameterType).SequenceEqual(parameters.Select(y => y.GetType())));
            
            method?.Invoke(spupComponent, parameters);
        }
        
        public static T GetField<T>(Component spupComponent, ref FieldInfo field, GettableFieldID fieldName)
		{
			if (!spupComponent) 
				return default;
			
			field ??= spupComponent
				.GetType()
				.GetFields()
				.FirstOrDefault(x => x.Name == fieldName.ToString());
				
			return (T) field?.GetValue(spupComponent);
		}

		public static void SetField(Component spupComponent, ref FieldInfo field, SettableFieldID fieldName, object value)
		{
			if (!spupComponent) 
				return;
			
			field ??= spupComponent
				.GetType()
				.GetFields()
				.FirstOrDefault(x => x.Name == fieldName.ToString());
				
			field?.SetValue(spupComponent, value);
		}
		
		public static T GetProperty<T>(Component spupComponent, ref PropertyInfo property, GettablePropertyID propertyName)
		{
			if (!spupComponent) 
				return default;
			
			property ??= spupComponent
				.GetType()
				.GetProperties()
				.FirstOrDefault(x => x.Name == propertyName.ToString());
				
			return (T) property?.GetValue(spupComponent);
		}
		
		public static void SetProperty(Component spupComponent, ref PropertyInfo property, SettablePropertyID propertyName, object value)
		{
			if (!spupComponent) 
				return;
			
			property ??= spupComponent
				.GetType()
				.GetProperties()
				.FirstOrDefault(x => x.Name == propertyName.ToString());
				
			property?.SetValue(spupComponent, value);
		}
        
		public static DeviceInfo[] GetConnectedDeviceList(OpenSystem deviceFormat)
		{
			if (!Application.isEditor && 
			    Application.platform != RuntimePlatform.WindowsPlayer &&
			    Application.platform != RuntimePlatform.Android)
				return Array.Empty<DeviceInfo>();

#if UNITY_ANDROID && !UNITY_EDITOR
			DeviceInfo[] deviceInfo = null;

			static AndroidJavaObject GetUnityJavaContext()
			{
				var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
			}

			switch (deviceFormat)
			{
				case OpenSystem.Usb:
				{
					var spLibClass = new AndroidJavaClass("com.wizapply.libspap.spap");
					// Get Context(Activity) Object
					var unityContext = GetUnityJavaContext();

					var usbList = spLibClass.CallStatic<string>("GetUSBConnection", unityContext);
					if (usbList.Length == 0)
						return null;

					var deviceKind = usbList.Split(';');
					var dKLen = deviceKind.Length - 1;
					deviceInfo = new DeviceInfo[dKLen];
					for(var i = 0; i < dKLen; ++i)
					{
						var dat = deviceKind[i].Split(',');
						deviceInfo[i] = new DeviceInfo
						{
							Vendor = dat[0], // VID
							Product = dat[1], // PID
							SerialNumber = dat[2], // SerialNumber
							PortName = ""
						};
					}

					break;
				}
				case OpenSystem.Pci:
				{
					var spLibClass = new AndroidJavaClass("com.wizapply.libspap.spap");
					// Get Context(Activity) Object
					var unityContext = GetUnityJavaContext();
					var usbList = spLibClass.CallStatic<string>("GetPCIConnection", unityContext);
					if (usbList.Length == 0)
						return null;

					var deviceKind = usbList.Split(';');
					var dKLen = deviceKind.Length - 1;
					deviceInfo = new DeviceInfo[dKLen];
					for (var i = 0; i < dKLen; ++i)
					{
						var datu = deviceKind[i].Split(',');
						deviceInfo[i] = new DeviceInfo
						{
							Vendor = datu[0], // VID
							Product = "", // PID
							SerialNumber = "", // SerialNumber
							PortName = datu[0]
						};
					}

					break;
				}
				case OpenSystem.BluetoothSsp:
				{
					var androidPlugin = new AndroidJavaClass("com.wizapply.libspap.spap");
					// Get Context(Activity) Object
					var unityContext = GetUnityJavaContext();
					var usbList = androidPlugin.CallStatic<string>("GetBluetoothConnection", unityContext);
					if (usbList.Length == 0)
						return null;

					var deviceKind = usbList.Split(';');
					var dKLen = deviceKind.Length - 1;
					deviceInfo = new DeviceInfo[dKLen];
					for (var i = 0; i < dKLen; ++i)
					{
						var dat = deviceKind[i].Split(',');
						deviceInfo[i] = new DeviceInfo
						{
							SerialNumber = dat[0] // SerialNumber
						};
					}

					break;
				}
				default:
					MetaverseProgram.Logger.Log(deviceFormat + ": GetConnectedDeviceList is not supported.");
					break;
			}
#else
			var deviceNum = spapDeviceListAvailable();
			var deviceString = new System.Text.StringBuilder[deviceNum];
			var deviceKind = new int[deviceNum];
			for (var i = 0; i < deviceNum; i++)
			{
				deviceString[i] = new System.Text.StringBuilder(1024);
				deviceKind[i] = spapDeviceList(i, deviceString[i], 1024);
			}

			// length
			var deviceInfoNum = 0;
			for (var i = 0; i < deviceNum; i++)
			{
				var openMethod = (int)deviceFormat;
				var dat = deviceString[i].ToString().Split(',');
				if (openMethod != deviceKind[i]) 
					continue;
				
				if (dat[0] == "null")
					continue;

				deviceInfoNum++;
			}

			var di = 0;
			var deviceInfo = new DeviceInfo[deviceInfoNum];
			for (var i = 0; i < deviceNum; i++)
			{
				var openMethod = (int)deviceFormat;
				var dat = deviceString[i].ToString().Split(',');
				if (openMethod != deviceKind[i]) 
					continue;
				
				if (dat[0] == "null")
					continue;

				switch (deviceFormat)
				{
					case OpenSystem.Usb:
						deviceInfo[di] = new DeviceInfo
						{
							Vendor = dat[0],
							Product = dat[1],
							SerialNumber = dat[2],
							PortName = dat[3]
						};
						break;
					case OpenSystem.Pci:
						deviceInfo[di] = new DeviceInfo
						{
							Vendor = dat[0],
							Product = dat[1],
							SerialNumber = "",
							PortName = dat[2]
						};
						break;
					case OpenSystem.BluetoothSsp:
						deviceInfo[di] = new DeviceInfo
						{
							Vendor = "",
							Product = "",
							SerialNumber = dat[0],
							PortName = dat[0]
						};
						break;
				}

				di++;
			}

#endif
			return deviceInfo;
		}
    
#if UNITY_EDITOR_WIN || UNITY_ANDROID || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		private static extern int spapDeviceListAvailable();
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		private static extern int spapDeviceList(int deviceNum, [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder deviceInfo, int bufferSize);
#else
		private static int spapDeviceListAvailable() => 0;
		private static int spapDeviceList(int deviceNum, System.Text.StringBuilder deviceInfo, int bufferSize) => 0;
#endif
	    public static void AddSystemEventCallback(
		    Component serialPortUtilityPro,
		    ref FieldInfo systemEventObjectField,
		    UnityAction<object, string> callback,
		    ref UnityAction<object, string> callbackCache) =>
		    AddSystemListener(ref callbackCache, ref systemEventObjectField, serialPortUtilityPro, callback);

	    public static void RemoveSystemEventCallback(
		    Component serialPortUtilityPro,
		    ref FieldInfo onSystemEventField,
		    UnityAction<object, string> onSystemEventCallback) => 
		    RemoveSystemListener(onSystemEventCallback, ref onSystemEventField, serialPortUtilityPro);

		private static void AddSystemListener(
		    ref UnityAction<object, string> delegateCall,
		    ref FieldInfo systemEventObjectField,
		    Component spupComponent,
		    UnityAction<object, string> callback)
		{
			if (!spupComponent)
			{
				MetaverseProgram.Logger.LogWarning("AddSystemListener: SerialPortUtilityPro component is null");
				return;
			}
			
		    if (delegateCall is not null)
		        RemoveSystemListener(delegateCall, ref systemEventObjectField, spupComponent);

		    var readCompleteEvent = GetField<UnityEventBase>(spupComponent, ref systemEventObjectField, GettableFieldID.SystemEventObject);
		    if (readCompleteEvent == null)
		    {
			    MetaverseProgram.Logger.LogError("AddSystemListener: Could not find SystemEventObject field in SerialPortUtilityPro component");
			    return;
		    }
		    
		    var eventType = readCompleteEvent.GetType().BaseType?.GetGenericArguments()[0];
		    if (eventType == null)
		    {
			    MetaverseProgram.Logger.LogError("AddSystemListener: Could not determine event type");
			    return;
		    }
		    
			const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public;
		    var addListenerCallFunction = readCompleteEvent.GetType().BaseType?.GetMethods(bindingFlags)
			    .FirstOrDefault(x => x.Name.Contains("AddListener", StringComparison.OrdinalIgnoreCase) && x.GetParameters().Length == 1);
		    if (addListenerCallFunction == null)
		    {
			    MetaverseProgram.Logger.LogError(
				    "AddSystemListener: Could not find AddListener method in " + readCompleteEvent.GetType().BaseType?.FullName + " | " +
			                                     string.Join(", ", readCompleteEvent.GetType().BaseType?.GetMethods(bindingFlags).Select(x => x.Name) ?? Array.Empty<string>()));
			    return;
		    }
		    
		    MetaverseProgram.Logger.Log("AddSystemListener: Adding listener to " + addListenerCallFunction.Name + " in " + readCompleteEvent.GetType().BaseType?.FullName);

		    var dynamicDelegate = Delegate.CreateDelegate(
		        typeof(UnityAction<,>).MakeGenericType(eventType, typeof(string)),
		        callback.Target,
		        callback.Method
		    );

		    addListenerCallFunction.Invoke(readCompleteEvent, new object[] { dynamicDelegate });
		    delegateCall = callback;
		}

		private static void RemoveSystemListener(
			UnityAction<object, string> delegateCall, 
			ref FieldInfo systemEventObjectField, 
			Component spupComponent)
		{
			if (!spupComponent)
			{
				MetaverseProgram.Logger.LogWarning("RemoveSystemListener: SerialPortUtilityPro component is null");
				return;
			}
			
		    if (delegateCall is null) return;

		    var readCompleteEvent = GetField<UnityEventBase>(spupComponent, ref systemEventObjectField, GettableFieldID.SystemEventObject);
		    if (readCompleteEvent == null)
		    {
			    MetaverseProgram.Logger.LogError($"RemoveSystemListener: Could not find {GettableFieldID.SystemEventObject} field in SerialPortUtilityPro component");
			    return;
		    }
		    
		    var eventType = readCompleteEvent.GetType().BaseType?.GetGenericArguments()[0];
		    if (eventType == null)
		    {
			    MetaverseProgram.Logger.LogError("RemoveSystemListener: Could not determine event type");
			    return;
		    }

		    var removeListenerCallFunction = readCompleteEvent.GetType().BaseType?.GetMethods(BindingFlags.Instance | BindingFlags.Public)
			    .FirstOrDefault(x => x.Name.Contains("RemoveListener", StringComparison.OrdinalIgnoreCase) && x.GetParameters().Length == 1);
		    if (removeListenerCallFunction == null)
		    {
			    MetaverseProgram.Logger.LogError(
				    $"RemoveSystemListener: Could not find RemoveListener method in {readCompleteEvent.GetType().BaseType?.FullName}");
			    return;
		    }

		    // Dynamically create the correct delegate for removal
		    var dynamicDelegate = Delegate.CreateDelegate(
		        typeof(UnityAction<,>).MakeGenericType(eventType, typeof(string)),
		        delegateCall.Target,
		        delegateCall.Method
		    );
		    
		    removeListenerCallFunction.Invoke(readCompleteEvent, new object[] { dynamicDelegate });
		}
    }
}
