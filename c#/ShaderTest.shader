// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Sprites/DefaultColorFlash"
 {
     Properties
     {
        //_MainTex ：属性名
        //"Sprite Texture" ：在Inspector中显示名
        //"2D" ：类型
        //"white" ：默认值为白纹理
        //[PerRendererData] ：每个渲染器数据，每个渲染器都有自己的纹理
         [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
         _Color ("Tint", Color) = (1,1,1,1)
         _FlashColor ("Flash Color", Color) = (1,1,1,1)
         _FlashAmount ("Flash Amount",Range(0.0,1.0)) = 0.0
         [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
     }
 
     SubShader
     {
         Tags
         { 
            //"Queue"="Transparent" ：渲染队列，透明队列
             "Queue"="Transparent" 
             //"IgnoreProjector"="True" ：忽略投影器，不被投影器影响
             "IgnoreProjector"="True" 
             //"RenderType"="Transparent" ：渲染类型，透明类型
             "RenderType"="Transparent" 
             //"PreviewType"="Plane" ：预览类型，平面类型
             "PreviewType"="Plane"
             //"CanUseSpriteAtlas"="True" ：允许 Sprite Atlas 系统使用
             "CanUseSpriteAtlas"="True"
         }
         //背面剔除
         Cull Off
         //关闭光照
         Lighting Off
         //关闭深度写入
         ZWrite Off
         //关闭雾效
         Fog { Mode Off }
         //混合模式，一种常用的混合模式，将源颜色与目标颜色进行混合
         Blend One OneMinusSrcAlpha
 
         Pass
         {
         CGPROGRAM
             #pragma vertex vert
             #pragma fragment frag
             #pragma multi_compile DUMMY PIXELSNAP_ON
             #include "UnityCG.cginc"
             
             struct appdata_t
             {
                 float4 vertex   : POSITION;
                 float4 color    : COLOR;
                 float2 texcoord : TEXCOORD0;
             };
 
             struct v2f
             {
                 float4 vertex   : SV_POSITION;
                 fixed4 color    : COLOR;
                 half2 texcoord  : TEXCOORD0;
             };
             
             fixed4 _Color;
             fixed4 _FlashColor;
             float _FlashAmount;
 
             v2f vert(appdata_t IN)
             {
                 v2f OUT;
                 OUT.vertex = UnityObjectToClipPos(IN.vertex);
                 OUT.texcoord = IN.texcoord;
                 OUT.color = IN.color * _Color;
                 #ifdef PIXELSNAP_ON
                 OUT.vertex = UnityPixelSnap (OUT.vertex);
                 #endif
 
                 return OUT;
             }
 
             sampler2D _MainTex;
 
             fixed4 frag(v2f IN) : COLOR
             {
                 fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                 c.rgb = lerp(c.rgb,_FlashColor.rgb,_FlashAmount);
                 c.rgb *= c.a;
             
                 return c;
             }
         ENDCG
         }
     }
 }
