#version 450

#include "Utils/ToneMapping.glsl"

layout(set = 0, binding = 1) uniform texture2D colorTexture;
layout(set = 0, binding = 2) uniform sampler anisoSampler;

layout(location = 0) in vec2 fragUV;
layout(location = 0) out vec4 fragColor;

void main()
{
    fragColor = texture(sampler2D(colorTexture, anisoSampler), fragUV).rgba;
}
