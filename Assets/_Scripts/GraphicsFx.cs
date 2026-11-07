using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// The modern look, all set up in code (built-in pipeline + Post Processing Stack v2):
/// HDR cameras with SMAA, bloom (gems, nitro and glow trims shine), ACES colour grading, vignette and, on the higher
/// quality levels, ambient occlusion; crisp long-range shadows; a reflection probe so the glossy car paint reflects the sky.
/// Scales with Settings > Quality (QualitySettings level): lower levels drop AO and keep shorter shadows.
/// </summary>
public static class GraphicsFx
{
    static PostProcessVolume volume;

    static int Level => QualitySettings.GetQualityLevel();

    /// <summary>Lights, shadows, reflections and the global post FX volume for this scene.</summary>
    public static void SetupScene(GemDriftConfig cfg, Light sun, bool menu)
    {
        int q = Level;
        if (q >= 3)
        {
            QualitySettings.shadowDistance = q >= 5 ? 170f : q >= 4 ? 130f : 90f;
            QualitySettings.shadowCascades = q >= 4 ? 4 : 2;
            QualitySettings.shadowResolution = q >= 4 ? ShadowResolution.VeryHigh : ShadowResolution.High;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        }
        if (sun != null)
        {
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;
        }
        RenderSettings.reflectionIntensity = 0.85f;
        // linear lighting makes the procedural sky's horizon band yellow: thinner atmosphere keeps it blue
        if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_AtmosphereThickness"))
        {
            var sky = new Material(RenderSettings.skybox);
            sky.SetFloat("_AtmosphereThickness", 0.7f);
            sky.SetFloat("_Exposure", 1.05f);
            sky.SetColor("_GroundColor", new Color(0.36f, 0.42f, 0.44f));
            RenderSettings.skybox = sky;
        }

        // one big reflection probe, rendered once the course is built: sky + scenery in the car paint
        var probeGo = new GameObject("ReflectionProbe");
        probeGo.transform.position = new Vector3(0f, 6f, 0f);
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(4000f, 600f, 4000f);
        probe.resolution = q >= 4 ? 256 : 128;
        probe.hdr = true;
        probe.cullingMask = ~(1 << Layers.Car | 1 << Layers.Gem);
        probe.farClipPlane = 800f;
        probe.RenderProbe();

        BuildVolume(q, menu);
    }

    static void BuildVolume(int q, bool menu)
    {
        var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        bool hdrGrading = SystemInfo.supportsComputeShaders && SystemInfo.supports3DRenderTextures;

        var bloom = profile.AddSettings<Bloom>();
        bloom.enabled.Override(true);
        bloom.intensity.Override(hdrGrading ? 1.1f : 0.8f);
        bloom.threshold.Override(1.2f);
        bloom.softKnee.Override(0.55f);
        bloom.diffusion.Override(7.5f);
        bloom.fastMode.Override(q < 4);

        var grade = profile.AddSettings<ColorGrading>();
        grade.enabled.Override(true);
        if (hdrGrading)
        {
            grade.gradingMode.Override(GradingMode.HighDefinitionRange);
            grade.tonemapper.Override(Tonemapper.ACES);
            grade.postExposure.Override(0.3f);
            grade.contrast.Override(12f);
            grade.saturation.Override(0f);
        }
        else
        {
            // WebGL has no compute shaders: LDR grading, no tonemapper
            grade.gradingMode.Override(GradingMode.LowDefinitionRange);
            grade.contrast.Override(10f);
            grade.saturation.Override(0f);
            grade.brightness.Override(2f);
        }
        grade.temperature.Override(4f);

        var vignette = profile.AddSettings<Vignette>();
        vignette.enabled.Override(true);
        vignette.intensity.Override(menu ? 0.38f : 0.26f);
        vignette.smoothness.Override(0.45f);

        if (q >= 4)
        {
            var ao = profile.AddSettings<AmbientOcclusion>();
            ao.enabled.Override(true);
            bool msvo = SystemInfo.supportsComputeShaders;
            ao.mode.Override(msvo ? AmbientOcclusionMode.MultiScaleVolumetricObscurance : AmbientOcclusionMode.ScalableAmbientObscurance);
            ao.intensity.Override(msvo ? 0.85f : 0.6f);
            ao.radius.Override(0.6f);
            ao.quality.Override(AmbientOcclusionQuality.Medium);
        }

        var go = new GameObject("PostFX");
        volume = go.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
    }

    /// <summary>Gives a gameplay or menu camera HDR + SMAA + the post FX stack.</summary>
    public static Camera Apply(GemDriftConfig cfg, Component cameraOwner)
    {
        var cam = cameraOwner.GetComponent<Camera>();
        if (cam == null) return null;
        cam.allowHDR = true;
        cam.allowMSAA = false; // SMAA below instead: MSAA + HDR post is costly on WebGL
        if (cfg.postResources == null) return cam;

        var go = cam.gameObject;
        bool active = go.activeSelf;
        go.SetActive(false); // Init must run before the layer's OnEnable
        var layer = go.AddComponent<PostProcessLayer>();
        layer.Init(cfg.postResources);
        layer.volumeTrigger = cam.transform;
        layer.volumeLayer = ~0;
        layer.antialiasingMode = Level >= 2 ? PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing
                                            : PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
        layer.subpixelMorphologicalAntialiasing.quality = SubpixelMorphologicalAntialiasing.Quality.High;
        layer.stopNaNPropagation = true;
        go.SetActive(active);
        return cam;
    }
}
