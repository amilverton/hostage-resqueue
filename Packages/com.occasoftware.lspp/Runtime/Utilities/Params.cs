using UnityEngine;

namespace OccaSoftware.LSPP.Runtime
{
    internal static class Params
    {
        public readonly struct Param
        {
            public Param(string property)
            {
                Property = property;
                Id = Shader.PropertyToID(property);
            }

            readonly public string Property;
            readonly public int Id;
        }

        public static Param Density = new Param("_Density");
        public static Param DoSoften = new Param("_DoSoften");
        public static Param DoAnimate = new Param("_DoAnimate");
        public static Param MaxRayDistance = new Param("_MaxRayDistance");
        public static Param SampleCount = new Param("lspp_NumSamples");
        public static Param Tint = new Param("_Tint");
        public static Param LightOnScreenRequired = new Param("_LightOnScreenRequired");
        public static Param FalloffDirective = new Param("_FalloffDirective");
        public static Param FalloffIntensity = new Param("_FalloffIntensity");
        public static Param OcclusionAssumption = new Param("_OcclusionAssumption");
        public static Param OcclusionOverDistanceAmount = new Param("_OcclusionOverDistanceAmount");
    }
}
