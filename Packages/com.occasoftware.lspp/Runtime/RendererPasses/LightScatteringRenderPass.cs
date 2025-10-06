using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;

#if UNITY_2023_3_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace OccaSoftware.LSPP.Runtime
{
    internal sealed class LightScatteringRenderPass : ScriptableRenderPass
    {
        private Material occluderMaterial = null;
        private Material mergeMaterial = null;
        private Material blitMaterial = null;
        private Material lightScatterMaterial = null;

        private RTHandle source;
        private RTHandle occluderRT;
        private RTHandle lightScatteringRT;
        private RTHandle mergeRT;

        private const string profilerTag = "[LLSP]";
        private const string blendingTargetId = "_LLSPTarget";

        private static readonly int lsppMatrixIdValue = Shader.PropertyToID("lspp_Matrix_VP");
        private static readonly int sourceIdValue = Shader.PropertyToID("_Source");

        private const string occluderRtId = "_Occluders_LSPP";
        private const string lightScatterRtId = "_Scattering_LSPP";
        private const string mergeRtId = "_Merge_LSPP";
        private const string bufferPoolId = "LightScatteringPP";

        private const string lightScatterMaterialPath = "OccaSoftware/LSPP/LightScatter";
        private const string occluderMaterialPath = "OccaSoftware/LSPP/Occluders";
        private const string mergeMaterialPath = "OccaSoftware/LSPP/Merge";
        private const string blitMaterialPath = "OccaSoftware/LSPP/Blit";

        private LightScatteringPostProcess lspp;

        public LightScatteringRenderPass()
        {
            occluderRT = RTHandles.Alloc(Shader.PropertyToID(occluderRtId), name: occluderRtId);
            lightScatteringRT = RTHandles.Alloc(Shader.PropertyToID(lightScatterRtId), name: lightScatterRtId);
            mergeRT = RTHandles.Alloc(Shader.PropertyToID(mergeRtId), name: mergeRtId);
        }

        public void SetTarget(RTHandle colorHandle)
        {
            source = colorHandle;
        }

        internal void Setup()
        {
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        internal void SetupMaterials()
        {
            SetupMaterial(ref lightScatterMaterial, lightScatterMaterialPath);
            SetupMaterial(ref occluderMaterial, occluderMaterialPath);
            SetupMaterial(ref mergeMaterial, mergeMaterialPath);
            SetupMaterial(ref blitMaterial, blitMaterialPath);
        }

        internal void SetupMaterial(ref Material material, string path)
        {
            if (material != null)
                return;

            Shader s = Shader.Find(path);
            if (s != null)
            {
                material = CoreUtils.CreateEngineMaterial(s);
            }
        }

        internal bool HasAllMaterials()
        {
            if (lightScatterMaterial == null)
                return false;

            if (occluderMaterial == null)
                return false;

            if (mergeMaterial == null)
                return false;

            if (blitMaterial == null)
                return false;

            return true;
        }

        internal bool RegisterStackComponent()
        {
            lspp = VolumeManager.instance.stack.GetComponent<LightScatteringPostProcess>();

            if (lspp == null)
                return false;

            return lspp.IsActive();
        }
#if UNITY_2023_3_OR_NEWER
        private class PassData
        {
            internal TextureHandle source;
            internal Matrix4x4 projectionMatrix;
            internal Matrix4x4 worldToCameraMatrix;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder = renderGraph.AddUnsafePass<PassData>(profilerTag, out var passData))
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                TextureHandle rtHandle = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    ConfigurePass(cameraData.cameraTargetDescriptor),
                    blendingTargetId,
                    false
                );

                passData.source = resourceData.cameraColor;
                passData.projectionMatrix = cameraData.camera.projectionMatrix;
                passData.worldToCameraMatrix = cameraData.camera.worldToCameraMatrix;

                builder.UseTexture(resourceData.cameraColor, AccessFlags.ReadWrite);

                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }
        }

        private void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            ExecutePass(cmd, data.projectionMatrix, data.worldToCameraMatrix, data.source);
        }
#endif

#if UNITY_2023_3_OR_NEWER
        [Obsolete]
#endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigurePass(renderingData.cameraData.cameraTargetDescriptor);
        }

        public RenderTextureDescriptor ConfigurePass(RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor rtDescriptor = cameraTextureDescriptor;
            rtDescriptor.colorFormat = RenderTextureFormat.DefaultHDR;
            rtDescriptor.msaaSamples = 1;
            rtDescriptor.depthBufferBits = 0;
            rtDescriptor.sRGB = false;
            rtDescriptor.width = Mathf.Max(1, rtDescriptor.width);
            rtDescriptor.height = Mathf.Max(1, rtDescriptor.height);

            // Setup Merge Target
            RenderingUtilsHelper.ReAllocateIfNeeded(ref mergeRT, rtDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: mergeRtId);

            // Setup Light Scatter Target
            rtDescriptor.width = rtDescriptor.width >> 1;
            rtDescriptor.height = rtDescriptor.height >> 1;
            RenderingUtilsHelper.ReAllocateIfNeeded(ref lightScatteringRT, rtDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: lightScatterRtId);

            // Setup Occluder target.
            RenderTextureDescriptor occluderDescriptor = rtDescriptor;
            occluderDescriptor.colorFormat = RenderTextureFormat.R8;
            RenderingUtilsHelper.ReAllocateIfNeeded(ref occluderRT, occluderDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: occluderRtId);

            return rtDescriptor;
        }

        public override void OnCameraCleanup(CommandBuffer cmd) { }


#if UNITY_2023_3_OR_NEWER
        [Obsolete]
#endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            UnityEngine.Profiling.Profiler.BeginSample(profilerTag);

            // Setup commandbuffer
            CommandBuffer cmd = CommandBufferPool.Get(bufferPoolId);

            ExecutePass(cmd, renderingData.cameraData.camera.projectionMatrix, renderingData.cameraData.camera.worldToCameraMatrix, renderingData.cameraData.renderer.cameraColorTargetHandle);

            // Clean up command buffer
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
            UnityEngine.Profiling.Profiler.EndSample();
        }

        public void ExecutePass(CommandBuffer cmd, Matrix4x4 projectionMatrix, Matrix4x4 worldToCameraMatrix, RTHandle source)
        {
            // Early exit
            if (!HasAllMaterials())
                return;

            Shader.SetGlobalMatrix(
                 lsppMatrixIdValue,
                 projectionMatrix * worldToCameraMatrix
             );

            // Draw to occluder texture
            Blitter.BlitCameraTexture(cmd, source, occluderRT, occluderMaterial, 0);

            // Set up scattering data texture
            UpdateLSPPMaterial(cmd);

            void UpdateLSPPMaterial(CommandBuffer cmd)
            {
                cmd.SetGlobalFloat(Params.Density.Id, lspp.fogDensity.value * 0.1f);
                cmd.SetGlobalInt(Params.DoSoften.Id, BoolToInt(lspp.softenScreenEdges.value));
                cmd.SetGlobalInt(Params.DoAnimate.Id, BoolToInt(lspp.animateSamplingOffset.value));
                cmd.SetGlobalFloat(Params.MaxRayDistance.Id, lspp.maxRayDistance.value);
                cmd.SetGlobalInt(Params.SampleCount.Id, lspp.numberOfSamples.value);
                cmd.SetGlobalColor(Params.Tint.Id, lspp.tint.value);
                cmd.SetGlobalInt(Params.LightOnScreenRequired.Id, BoolToInt(lspp.lightMustBeOnScreen.value));
                cmd.SetGlobalInt(Params.FalloffDirective.Id, (int)lspp.falloffBasis.value);
                cmd.SetGlobalFloat(Params.FalloffIntensity.Id, lspp.falloffIntensity.value);
                cmd.SetGlobalFloat(Params.OcclusionAssumption.Id, lspp.occlusionAssumption.value);
                cmd.SetGlobalFloat(Params.OcclusionOverDistanceAmount.Id, lspp.occlusionOverDistanceAmount.value);

                static int BoolToInt(bool a)
                {
                    return a == false ? 0 : 1;
                }
            }
            cmd.SetGlobalTexture(occluderRtId, occluderRT);
            Blitter.BlitCameraTexture(cmd, source, lightScatteringRT, lightScatterMaterial, 0);

            // Blit to Merge Target
            cmd.SetGlobalTexture(sourceIdValue, source);
            cmd.SetGlobalTexture(lightScatterRtId, lightScatteringRT);
            Blitter.BlitCameraTexture(cmd, source, mergeRT, mergeMaterial, 0);

            // Blit to screen
            cmd.SetGlobalTexture(sourceIdValue, mergeRT);
            Blitter.BlitCameraTexture(cmd, mergeRT, source, blitMaterial, 0);
        }
    }
}
