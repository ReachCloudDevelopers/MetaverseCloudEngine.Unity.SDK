using UnityEngine;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.Vehicles
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.MenuItems.MenuRootPath + "Vehicles/Land/Demo Scripts/Vehicles - Vehicle Menu", 0)]

    // Class for the menu in the demo
    public class VehicleMenu : MonoBehaviour
    {
        public CameraControl cam;
        public Vector3 spawnPoint;
        public Vector3 spawnRot;
        public GameObject[] vehicles;
        public GameObject chaseVehicle;
        public GameObject chaseVehicleDamage;
        float chaseCarSpawnTime;
        GameObject newVehicle;
        public Toggle autoShiftToggle;
        public Toggle assistToggle;
        public Toggle stuntToggle;
        public Toggle camToggle;
        public VehicleHud hud;

        void Update() {
            if (cam && camToggle)
                cam.stayFlat = camToggle.isOn;
            chaseCarSpawnTime = Mathf.Max(0, chaseCarSpawnTime - Time.deltaTime);
        }

        // Spawns a vehicle from the vehicles array at the index
        public void SpawnVehicle(int vehicle)
        {
            if (vehicle >= vehicles.Length || vehicle < 0 || !vehicles[vehicle])
            {
                Debug.Log("Vehicle does not exist.");
                return;
            }
            
            newVehicle = Instantiate(vehicles[vehicle], spawnPoint, Quaternion.LookRotation(spawnRot, Vector3.up)) as GameObject;
            if (cam)
            {
                cam.target = newVehicle.transform;
                cam.Initialize();   
            }

            if (newVehicle.GetComponent<VehicleAssist>()) {
                newVehicle.GetComponent<VehicleAssist>().enabled = assistToggle.isOn;
            }

            Transmission trans = newVehicle.GetComponentInChildren<Transmission>();
            if (trans) {
                trans.automatic = autoShiftToggle.isOn;
                newVehicle.GetComponent<VehicleParent>().brakeIsReverse = autoShiftToggle.isOn;

                if (trans is ContinuousTransmission && !autoShiftToggle.isOn) {
                    newVehicle.GetComponent<VehicleParent>().brakeIsReverse = true;
                }
            }

            if (newVehicle.GetComponent<FlipControl>() && newVehicle.GetComponent<StuntDetect>()) {
                newVehicle.GetComponent<FlipControl>().flipPower = stuntToggle.isOn && assistToggle.isOn ? new Vector3(10, 10, -10) : Vector3.zero;
                newVehicle.GetComponent<FlipControl>().rotationCorrection = stuntToggle.isOn ? Vector3.zero : (assistToggle.isOn ? new Vector3(5, 1, 10) : Vector3.zero);
                newVehicle.GetComponent<FlipControl>().stopFlip = assistToggle.isOn;
            }

            if (hud) {
                hud.stuntMode = stuntToggle.isOn;
                hud.Initialize(newVehicle);
            }
        }

        // Spawns a chasing vehicle
        public void SpawnChaseVehicle() {
            if (chaseCarSpawnTime == 0) {
                chaseCarSpawnTime = 1;
                GameObject chaseCar = Instantiate(chaseVehicle, spawnPoint, Quaternion.LookRotation(spawnRot, Vector3.up)) as GameObject;
                chaseCar.GetComponent<FollowAI>().target = newVehicle.transform;
            }
        }

        // Spawns a damageable chasing vehicle
        public void SpawnChaseVehicleDamage() {
            if (chaseCarSpawnTime == 0) {
                chaseCarSpawnTime = 1;
                GameObject chaseCar = Instantiate(chaseVehicleDamage, spawnPoint, Quaternion.LookRotation(spawnRot, Vector3.up)) as GameObject;
                chaseCar.GetComponent<FollowAI>().target = newVehicle.transform;
            }
        }
    }
}