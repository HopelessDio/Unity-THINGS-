using UnityEngine;

namespace Survivor.Player
{
    /// <summary>Independent weapon visuals that follow the animated hand sockets.</summary>
    public sealed class PlayerWeaponEquipment : MonoBehaviour
    {
        [SerializeField] private Transform swordSocket;
        [SerializeField] private Transform gunSocket;
        [SerializeField] private GameObject equippedSword;
        [SerializeField] private GameObject equippedGun;
        [SerializeField] private Transform gunMuzzle;
        [SerializeField] private Transform swordOffhandGrip;

        public Transform SwordSocket => swordSocket;
        public Transform GunSocket => gunSocket;
        public Transform GunMuzzle => gunMuzzle;
        public Transform SwordOffhandGrip => swordOffhandGrip;
        public GameObject EquippedSword => equippedSword;
        public GameObject EquippedGun => equippedGun;

        public void ConfigureSockets(Transform swordHand, Transform gunHand)
        {
            swordSocket = swordHand;
            gunSocket = gunHand;
        }

        public void ConfigureSwordSupport(Transform supportGrip)
        {
            swordOffhandGrip = supportGrip;
        }

        public void EquipSword(GameObject weaponPrefab)
        {
            ReplaceWeapon(ref equippedSword, weaponPrefab, swordSocket);
        }

        public void EquipGun(GameObject weaponPrefab)
        {
            ReplaceWeapon(ref equippedGun, weaponPrefab, gunSocket);
            gunMuzzle = null;
            if (equippedGun == null) return;
            foreach (Transform child in equippedGun.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Muzzle")
                {
                    gunMuzzle = child;
                    break;
                }
            }
        }

        private static void ReplaceWeapon(ref GameObject current, GameObject prefab, Transform socket)
        {
            if (prefab != null && socket == null)
            {
                Debug.LogError("Cannot equip a weapon without a hand socket.");
                return;
            }
            if (current != null)
            {
                current.SetActive(false);
                if (Application.isPlaying) Destroy(current);
                else DestroyImmediate(current);
                current = null;
            }
            if (prefab == null) return;
            current = Instantiate(prefab, socket, false);
            current.name = prefab.name;
            current.transform.localPosition = Vector3.zero;
            current.transform.localRotation = Quaternion.identity;
            current.transform.localScale = Vector3.one;
        }
    }
}
