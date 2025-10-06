using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

using UnityEngine.Rendering.Universal;
#if UNITY_2023_3_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace OccaSoftware.LSPP.Runtime
{
    public class LightScatteringRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public Settings settings = new Settings();
        LightScatteringRenderPass lightScatteringPass;

        public override void Create()
        {
            lightScatteringPass = new LightScatteringRenderPass();
            lightScatteringPass.renderPassEvent = settings.renderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.camera.cameraType == CameraType.Reflection)
                return;

            if (renderingData.cameraData.camera.cameraType == CameraType.Preview)
                return;

            if (!renderingData.cameraData.postProcessEnabled)
                return;

            if (!lightScatteringPass.RegisterStackComponent())
                return;

            lightScatteringPass.SetupMaterials();
            if (!lightScatteringPass.HasAllMaterials())
                return;

            renderer.EnqueuePass(lightScatteringPass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            lightScatteringPass.Setup();
            lightScatteringPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }
    }
}
