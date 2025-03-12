#import <Foundation/Foundation.h>
#import <CoreBluetooth/CoreBluetooth.h>
#import <stdio.h>
#import <string.h>

// ---------------------------------------------------------------------
// SPAPBluetoothManager: A singleton manager that uses CoreBluetooth
// to continuously scan for peripherals and cache discovered devices.
@interface SPAPBluetoothManager : NSObject <CBCentralManagerDelegate>
@property (nonatomic, strong) CBCentralManager *centralManager;
@property (nonatomic, strong) NSMutableArray<CBPeripheral *> *discoveredPeripherals;
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
        // Initialize the central manager on the main queue.
        _centralManager = [[CBCentralManager alloc] initWithDelegate:self queue:dispatch_get_main_queue()];
    }
    return self;
}

- (void)startScan {
    // Start scanning and do not stop, so the cache updates continually.
    if (self.centralManager.state == CBManagerStatePoweredOn) {
        [self.centralManager scanForPeripheralsWithServices:nil options:nil];
    }
}

#pragma mark - CBCentralManagerDelegate

- (void)centralManagerDidUpdateState:(CBCentralManager *)central {
    if (central.state == CBManagerStatePoweredOn) {
        [self startScan];
    } else {
        // Optionally handle other states (powered off, unauthorized, etc.)
    }
}

- (void)centralManager:(CBCentralManager *)central
 didDiscoverPeripheral:(CBPeripheral *)peripheral
     advertisementData:(NSDictionary<NSString *,id> *)advertisementData
                  RSSI:(NSNumber *)RSSI {
    // Add the peripheral to the cache if not already present.
    if (![self.discoveredPeripherals containsObject:peripheral]) {
        [self.discoveredPeripherals addObject:peripheral];
    }
}

@end

// ---------------------------------------------------------------------
// C-exported functions for Unity to call.
// These functions allow Unity to retrieve the cached device list and start scanning.

#ifdef __cplusplus
extern "C" {
#endif

// Starts the Bluetooth scan.
// This function is called by the runtime initializer in C#.
void ios_startScan(void) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    [manager startScan];
}

// Returns the number of discovered Bluetooth devices from the cache.
int ios_spapDeviceListAvailable(void) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    return (int)[manager.discoveredPeripherals count];
}

// Fills the provided buffer with device info for the device at the given index.
// The string format is: Vendor,Product,SerialNumber,PortName
// For Bluetooth devices on iOS, we leave Vendor and Product empty,
// use the peripheral’s UUID as SerialNumber, and the peripheral’s name (or "Unknown") as PortName.
int ios_spapDeviceList(int deviceNum, char *deviceInfo, int bufferSize) {
    SPAPBluetoothManager *manager = [SPAPBluetoothManager sharedManager];
    if (deviceNum < [manager.discoveredPeripherals count]) {
        CBPeripheral *peripheral = manager.discoveredPeripherals[deviceNum];
        NSString *serialNumber = peripheral.identifier.UUIDString;
        NSString *portName = peripheral.name ? peripheral.name : @"Unknown";
        // Format: Vendor,Product,SerialNumber,PortName
        NSString *info = [NSString stringWithFormat:@",,%@,%@", serialNumber, portName];
        const char *cInfo = [info UTF8String];
        strncpy(deviceInfo, cInfo, bufferSize);
        deviceInfo[bufferSize - 1] = '\0'; // Ensure null termination.
        // Return the integer value corresponding to BluetoothSsp (3).
        return 3;
    }
    return -1;
}

#ifdef __cplusplus
}
#endif
