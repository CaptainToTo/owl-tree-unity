using UnityEngine;

namespace OwlTree.Unity
{
    public static class VectorExtensions
    {

        public static Vector3 ToVec3(this NetworkVec3 vec) => new Vector3(vec.x, vec.y, vec.z);

        public static NetworkVec3 ToNetVec3(this Vector3 vec) => new NetworkVec3(vec.x, vec.y, vec.z);

        public static Color ToColor(this NetworkVec3 vec) => new Color(vec.x, vec.y, vec.z);

        public static NetworkVec3 ToNetVec3(this Color col) => new NetworkVec3(col.r, col.g, col.b);

        public static Vector2 ToVec2(this NetworkVec2 vec) => new Vector2(vec.x, vec.y);

        public static NetworkVec2 ToNetVec2(this Vector2 vec) => new NetworkVec2(vec.x, vec.y);

        public static NetworkVec2 ToNetVec2(this Vector3 vec) => new NetworkVec2(vec.y, vec.y);

        public static Quaternion ToQuat(this NetworkVec4 vec) => new Quaternion(vec.x, vec.y, vec.z, vec.w);

        public static NetworkVec4 ToNetVec4(this Quaternion quat) => new NetworkVec4(quat.x, quat.y, quat.z, quat.w);
    }
}
