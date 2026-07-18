// ============================================================================
//  CharacterCutout.shader —— 角色骨骼版专用 sprite shader,治白线用
//
//  【背景】老年 psb 里每个部位 Sprite 的 alpha 边缘(半透明像素)RGB 都是 145~212
//  的浅灰白(诊断结论,tools\诊断输出可见)。默认 Sprites-Default 是 alpha blend,
//  半透明的白像素混到深色背景/相邻部位边上 → 一圈浅白亮线,朝向翻转时跟着走。
//
//  【方案】改用 alpha 阈值裁剪:alpha < _Cutoff 的像素直接 discard。
//  半透明白边直接被切掉,只留 alpha 高的实体像素。零改 psb、零重绑骨骼。
//  _Cutoff 默认 0.75,想更狠调到 0.85,想手感更软调到 0.5。
//
//  【为什么不 alpha to coverage / MSAA】内置管线 sprite 走透明队列,MSAA 对透明
//  物件不起作用。cutout 是最省事、跨平台稳的方案,和 Sprites-Default 一样支持
//  SpriteRenderer.color(挂到 SpriteSkin/Animator 都无缝)。
//
//  【用法】把 Material 用这个 shader 建出来 → 拖到骨骼版角色所有 SpriteRenderer 的
//  Material 槽(通过 CharacterCutoutApplier 一键批量)。
// ============================================================================
Shader "LostGoddess/CharacterCutout"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.75
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="TransparentCutout"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha   // 预乘 alpha 混合(sprite 标准)

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment CharacterFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnitySprites.cginc"

            fixed _Cutoff;

            fixed4 CharacterFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                // 关键:低于阈值(半透明白边)直接 discard,不进混合,不出亮线
                clip(c.a - _Cutoff);
                // 预乘,配 Blend One OneMinusSrcAlpha
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
