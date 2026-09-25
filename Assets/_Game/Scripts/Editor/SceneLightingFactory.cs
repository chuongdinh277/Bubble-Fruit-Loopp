#if UNITY_EDITOR
using UnityEngine;

namespace BubbleFruitLoop.Editor
{
    public static class SceneLightingFactory
    {
        public static void Create(Transform parent)
        {
            GameObject lightObject = new("Directional Light");
            lightObject.transform.SetParent(parent);
            lightObject.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            Light sceneLight = lightObject.AddComponent<Light>();
            sceneLight.type = LightType.Directional;
            sceneLight.intensity = 1.2f;
        }
    }
}
#endif
