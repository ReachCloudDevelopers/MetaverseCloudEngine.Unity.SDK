#import <Foundation/Foundation.h>
#import <CoreBluetooth/CoreBluetooth.h>
#import <stdio.h>
#import <string.h>

// ---------------------------------------------------------------------
// SPAPDiscoveredPeripheral: A helper class to store a peripheral and
// its last seen timestamp.
@interface SPAPDiscoveredPeripheral : NSObject
@property (nonatomic, strong) CBPeripheral *peripheral;
@property (nonatomic, strong) NSDate *lastSeen;
@end

@implementation SPAPDiscoveredPeripheral
@end

// ---------------------------------------------------------------------
// SPAPBluetoothManager: A singleton manager that uses CoreBluetooth
// to continuously scan for peripherals, connect/disconnect, and handle
// data transfer (write/read). The discovered peripherals are maintained
// in a dictionary keyed by UUID to ensure uniqueness and to allow stale
// entries to be purged.
@interface SPAPBluetoothManager : NSObject <CBCentralManagerDelegate, CBPeripheralDelegate>
@property (nonatomic, strong) CBCentralManager *centralManager;
// A dictionary mapping UUID strings to SPAPDiscoveredPeripheral objects.
@property (nonatomic, strong) NSMutableDictionary<NSString *, SPAPDiscoveredPeripheral *> *discoveredPeripherals;
@property (nonatomic, strong) CBPeripheral *connectedPeripheral;
@property (nonatomic, strong) CBCharacteristic *writeCharacteristic;
@property (nonatomic, strong) CBCharacteristic *readCharacteristic;
@property (nonatomic, strong) NSMutableData *incomingData;
+ (instancetype)sharedManager;
- (void)startScan;
// Purge peripherals not seen in the last 60 seconds.
- (void)purgeStalePeripherals;
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
        _discoveredPeripherals = [NSMutableDictionary dictionary];
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

// Purge peripherals that haven't been updated in the last 60 seconds.
- (void)purgeStalePeripherals {
    NSTimeInterval threshold = 60.0; // seconds
    NSDate *now = [NSDate date];
    NSMutableArray *keysToRemove = [NSMutableArray array];
    for (NSString *key in self.discoveredPeripherals) {
        SPAPDiscoveredPeripheral *dp = self.discoveredPeripherals[key];
        if ([now timeIntervalSinceDate:dp.lastSeen] > threshold) {
            [keysToRemove addObject:key];
        }
    }
    if ([keysToRemove count] > 0) {
        NSLog(@"SPAPBluetoothManager: Purging stale peripherals: %@", keysToRemove);
        [self.discoveredPeripherals removeObjectsForKeys:keysToRemove];
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
    NSString *uuid = peripheral.identifier.UUIDString;
    SPAPDiscoveredPeripheral *existing = self.discoveredPeripherals[uuid];
    if (existing) {
        // Update last seen time.
        existing.lastSeen = [NSDate date];
    } else {
        SPAPDiscoveredPeripheral *newEntry = [[SPAPDiscoveredPeripheral alloc] init];
        newEntry.peripheral = peripheral;
        newEntry.lastSeen = [NSDate date];
        self.discoveredPeripherals[uuid] = newEntry;
        peripheral.delegate = self;
        NSLog(@"SPAPBluetoothManager: Discovered peripheral: %@, UUID: %@", peripheral.name, uuid);
    }
}

- (void)centralManager:(CBCentralManager *)central didConnectPeripheral:(CBPeripheral *)peripheral {
    NSLog(@"SPAPBluetoothManager: Connected to peripheral: %@, UUID: %@", peripheral.name, peripheral.identifier.UUIDString);
    self.connectedPeripheral = peripheral;
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
        if ((characteristic.properties & CBCharacteristicPropertyWrite) ||
            (characteristic.properties & CBCharacteristicPropertyWriteWithoutResponse)) {
            self.writeCharacteristic = characteristic;
            NSLog(@"SPAPBluetoothManager: Assigned writeCharacteristic: %@", characteristic.UUID.UUIDString);
        }
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
// Before returning, purge stale entries.
int ios_spapDeviceListAvailable(void) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    [manager purgeStalePeripherals];
    return (int)[manager.discoveredPeripherals count];
}

// Fills the provided buffer with device info for the device at the given index.
// For iOS Bluetooth devices, we output a single token string (the device's UUID)
// since the C# side expects dat[0] to contain the SerialNumber.
int ios_spapDeviceList(int deviceNum, char *deviceInfo, int bufferSize) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    [manager purgeStalePeripherals];
    // Get all discovered devices (as SPAPDiscoveredPeripheral objects) sorted by lastSeen.
    NSArray *allDevices = [[manager.discoveredPeripherals allValues] sortedArrayUsingComparator:^NSComparisonResult(SPAPDiscoveredPeripheral *obj1, SPAPDiscoveredPeripheral *obj2) {
        return [obj1.lastSeen compare:obj2.lastSeen];
    }];
    if (deviceNum < [allDevices count]) {
        SPAPDiscoveredPeripheral *dp = allDevices[deviceNum];
        CBPeripheral *peripheral = dp.peripheral;
        // Output a single token: the device's UUID.
        NSString *token = peripheral.identifier.UUIDString;
        const char *cInfo = [token UTF8String];
        strncpy(deviceInfo, cInfo, bufferSize);
        deviceInfo[bufferSize - 1] = '\0'; // Ensure null termination.
        // Return BluetoothSsp (3) to indicate the open method.
        return 3;
    }
    return -1;
}

// Connects to a Bluetooth device identified by its serial number (UUID).
// Returns 0 on success, -1 if no matching device is found.
int ios_connectToDevice(const char *serialNumber) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    NSString *targetUUID = [NSString stringWithUTF8String:serialNumber];
    [manager purgeStalePeripherals];
    for (NSString *key in manager.discoveredPeripherals) {
        SPAPDiscoveredPeripheral *dp = manager.discoveredPeripherals[key];
        if ([dp.peripheral.identifier.UUIDString isEqualToString:targetUUID]) {
            NSLog(@"ios_connectToDevice: Found matching peripheral: %@", dp.peripheral.name);
            [manager.centralManager connectPeripheral:dp.peripheral options:nil];
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
    [manager.connectedPeripheral writeValue:dataToWrite forCharacteristic:manager.writeCharacteristic type:CBCharacteristicWriteWithResponse];
    NSLog(@"ios_writeData: Wrote %d bytes.", length);
    return length;
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
    NSRange range = NSMakeRange(0, bytesToCopy);
    [manager.incomingData replaceBytesInRange:range withBytes:NULL length:0];
    NSLog(@"ios_readData: Read %d bytes.", bytesToCopy);
    return bytesToCopy;
}

#ifdef __cplusplus
}
#endif
