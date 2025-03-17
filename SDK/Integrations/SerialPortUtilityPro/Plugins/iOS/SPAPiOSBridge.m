#import <Foundation/Foundation.h>
#import <CoreBluetooth/CoreBluetooth.h>
#import <stdio.h>
#import <string.h>

// ---------------------------------------------------------------------
// SPAPBluetoothManager: A singleton manager that uses CoreBluetooth
// to continuously scan for peripherals, connect/disconnect, and handle
// data transfer (write/read).
@interface SPAPBluetoothManager : NSObject <CBCentralManagerDelegate, CBPeripheralDelegate>
@property (nonatomic, strong) CBCentralManager *centralManager;
@property (nonatomic, strong) NSMutableArray<CBPeripheral *> *discoveredPeripherals;
@property (nonatomic, strong) CBPeripheral *connectedPeripheral;
@property (nonatomic, strong) CBCharacteristic *writeCharacteristic;
@property (nonatomic, strong) CBCharacteristic *readCharacteristic;
@property (nonatomic, strong) NSMutableData *incomingData;
+ (instancetype)sharedManager;
- (void)startScan;
@end

@implementation SPAPBluetoothManager

+ (instancetype)sharedManager {
    static SPAPBluetoothManager *manager = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        manager = [[SPAPBluetoothManager alloc] init];
    });
    return manager;
}

- (instancetype)init {
    if (self = [super init]) {
        _discoveredPeripherals = [NSMutableArray array];
        _incomingData = [NSMutableData data];
        _centralManager = [[CBCentralManager alloc] initWithDelegate:self queue:dispatch_get_main_queue()];
    }
    return self;
}

- (void)startScan {
    if (self.centralManager.state == CBManagerStatePoweredOn) {
        NSLog(@"SPAPBluetoothManager: Starting scan...");
        [self.centralManager scanForPeripheralsWithServices:nil options:nil];
    }
}

#pragma mark - CBCentralManagerDelegate

- (void)centralManagerDidUpdateState:(CBCentralManager *)central {
    if (central.state == CBManagerStatePoweredOn) {
        [self startScan];
    } else {
        NSLog(@"SPAPBluetoothManager: Central Manager state changed: %ld", (long)central.state);
    }
}

- (void)centralManager:(CBCentralManager *)central 
 didDiscoverPeripheral:(CBPeripheral *)peripheral 
     advertisementData:(NSDictionary<NSString *,id> *)advertisementData 
                  RSSI:(NSNumber *)RSSI {
    if (![self.discoveredPeripherals containsObject:peripheral]) {
        [self.discoveredPeripherals addObject:peripheral];
        peripheral.delegate = self;
        NSLog(@"SPAPBluetoothManager: Discovered peripheral: %@, UUID: %@", peripheral.name, peripheral.identifier.UUIDString);
    }
}

- (void)centralManager:(CBCentralManager *)central didConnectPeripheral:(CBPeripheral *)peripheral {
    NSLog(@"SPAPBluetoothManager: Connected to peripheral: %@, UUID: %@", peripheral.name, peripheral.identifier.UUIDString);
    self.connectedPeripheral = peripheral;
    // Discover services; here you could provide specific UUIDs if known.
    [peripheral discoverServices:nil];
}

- (void)centralManager:(CBCentralManager *)central didFailToConnectPeripheral:(CBPeripheral *)peripheral error:(NSError *)error {
    NSLog(@"SPAPBluetoothManager: Failed to connect to peripheral: %@, error: %@", peripheral.name, error);
}

- (void)centralManager:(CBCentralManager *)central didDisconnectPeripheral:(CBPeripheral *)peripheral error:(NSError *)error {
    NSLog(@"SPAPBluetoothManager: Disconnected from peripheral: %@, error: %@", peripheral.name, error);
    if (self.connectedPeripheral == peripheral) {
        self.connectedPeripheral = nil;
        self.writeCharacteristic = nil;
        self.readCharacteristic = nil;
        [self.incomingData setLength:0];
    }
}

#pragma mark - CBPeripheralDelegate

- (void)peripheral:(CBPeripheral *)peripheral didDiscoverServices:(NSError *)error {
    if (error) {
        NSLog(@"SPAPBluetoothManager: Error discovering services: %@", error);
        return;
    }
    for (CBService *service in peripheral.services) {
        NSLog(@"SPAPBluetoothManager: Discovered service: %@", service.UUID.UUIDString);
        // Discover all characteristics for this service.
        [peripheral discoverCharacteristics:nil forService:service];
    }
}

- (void)peripheral:(CBPeripheral *)peripheral didDiscoverCharacteristicsForService:(CBService *)service error:(NSError *)error {
    if (error) {
        NSLog(@"SPAPBluetoothManager: Error discovering characteristics: %@", error);
        return;
    }
    for (CBCharacteristic *characteristic in service.characteristics) {
        NSLog(@"SPAPBluetoothManager: Discovered characteristic: %@", characteristic.UUID.UUIDString);
        // Assign a write characteristic if available.
        if ((characteristic.properties & CBCharacteristicPropertyWrite) ||
            (characteristic.properties & CBCharacteristicPropertyWriteWithoutResponse)) {
            self.writeCharacteristic = characteristic;
            NSLog(@"SPAPBluetoothManager: Assigned writeCharacteristic: %@", characteristic.UUID.UUIDString);
        }
        // Assign a notify characteristic for reading data.
        if (characteristic.properties & CBCharacteristicPropertyNotify) {
            self.readCharacteristic = characteristic;
            [peripheral setNotifyValue:YES forCharacteristic:characteristic];
            NSLog(@"SPAPBluetoothManager: Assigned readCharacteristic: %@", characteristic.UUID.UUIDString);
        }
    }
}

- (void)peripheral:(CBPeripheral *)peripheral didUpdateValueForCharacteristic:(CBCharacteristic *)characteristic error:(NSError *)error {
    if (error) {
        NSLog(@"SPAPBluetoothManager: Error receiving data: %@", error);
        return;
    }
    if (characteristic.value) {
        [self.incomingData appendData:characteristic.value];
        NSLog(@"SPAPBluetoothManager: Received data: %@", characteristic.value);
    }
}

@end

// ---------------------------------------------------------------------
// C-exported functions for Unity to call.
// These functions are linked with the DllImport calls from your C# layer.
#ifdef __cplusplus
extern "C" {
#endif

// Starts the Bluetooth scan.
void ios_startScan(void) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    [manager startScan];
    NSLog(@"ios_startScan: Scan started.");
}

// Returns the number of discovered Bluetooth devices.
int ios_spapDeviceListAvailable(void) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    return (int)[manager.discoveredPeripherals count];
}

// Fills the provided buffer with device info for the device at the given index.
// For iOS Bluetooth devices, we output a single token (the device's UUID).
// The expected format for BluetoothSsp (as per the C# code) is that dat[0] contains the SerialNumber.
int ios_spapDeviceList(int deviceNum, char *deviceInfo, int bufferSize) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    [manager purgeStalePeripherals];
    
    // Convert the dictionary values to an array and sort by lastSeen time.
    NSArray *allDevices = [[manager.discoveredPeripherals allValues] sortedArrayUsingComparator:^NSComparisonResult(SPAPDiscoveredPeripheral *obj1, SPAPDiscoveredPeripheral *obj2) {
        return [obj1.lastSeen compare:obj2.lastSeen];
    }];
    
    if (deviceNum < [allDevices count]) {
        SPAPDiscoveredPeripheral *dp = allDevices[deviceNum];
        CBPeripheral *peripheral = dp.peripheral;
        
        // For iOS Bluetooth devices, output a single token string: the device's UUID.
        NSString *token = peripheral.identifier.UUIDString;
        const char *cInfo = [token UTF8String];
        strncpy(deviceInfo, cInfo, bufferSize);
        deviceInfo[bufferSize - 1] = '\0'; // Ensure null termination.
        
        // Return the open method as BluetoothSsp (3).
        return 3;
    }
    return -1;
}

// Connects to a Bluetooth device identified by its serial number (UUID).
// Returns 0 on success, -1 if no matching device is found.
int ios_connectToDevice(const char *serialNumber) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    NSString *targetUUID = [NSString stringWithUTF8String:serialNumber];
    for (CBPeripheral *peripheral in manager.discoveredPeripherals) {
        if ([peripheral.identifier.UUIDString isEqualToString:targetUUID]) {
            NSLog(@"ios_connectToDevice: Found matching peripheral: %@", peripheral.name);
            [manager.centralManager connectPeripheral:peripheral options:nil];
            // For simplicity, assume connection is immediate.
            return 0;
        }
    }
    NSLog(@"ios_connectToDevice: No matching peripheral found for UUID: %@", targetUUID);
    return -1;
}

// Disconnects from the currently connected Bluetooth device.
void ios_disconnect(void) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    if (manager.connectedPeripheral) {
        [manager.centralManager cancelPeripheralConnection:manager.connectedPeripheral];
        NSLog(@"ios_disconnect: Disconnected from peripheral: %@", manager.connectedPeripheral.name);
    } else {
        NSLog(@"ios_disconnect: No connected peripheral to disconnect.");
    }
}

// Writes data to the connected Bluetooth device.
// Returns the number of bytes written, or -1 if not connected.
int ios_writeData(const uint8_t *data, int length) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    if (!manager.connectedPeripheral || !manager.writeCharacteristic) {
        NSLog(@"ios_writeData: Not connected or write characteristic unavailable.");
        return -1;
    }
    NSData *dataToWrite = [NSData dataWithBytes:data length:length];
    // Write with response. (The actual result is asynchronous.)
    [manager.connectedPeripheral writeValue:dataToWrite forCharacteristic:manager.writeCharacteristic type:CBCharacteristicWriteWithResponse];
    NSLog(@"ios_writeData: Wrote %d bytes.", length);
    return length; // Assume success.
}

// Reads data from the connected Bluetooth device.
// Copies up to bufferSize bytes into the provided buffer and returns the number of bytes read.
int ios_readData(uint8_t *buffer, int bufferSize) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    int availableBytes = (int)[manager.incomingData length];
    if (availableBytes == 0) {
        return 0;
    }
    int bytesToCopy = MIN(availableBytes, bufferSize);
    [manager.incomingData getBytes:buffer length:bytesToCopy];
    // Remove the copied bytes from the incoming data buffer.
    NSRange range = NSMakeRange(0, bytesToCopy);
    [manager.incomingData replaceBytesInRange:range withBytes:NULL length:0];
    NSLog(@"ios_readData: Read %d bytes.", bytesToCopy);
    return bytesToCopy;
}

#ifdef __cplusplus
}
#endif
