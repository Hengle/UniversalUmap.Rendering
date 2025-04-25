#version 450

#include "Utils/FXAA.glsl"

layout(set = 0, binding = 0) uniform texture2D textureColor;
layout(set = 0, binding = 1) uniform sampler samplerColor;

layout(location = 0) in vec2 fragUV;
layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 2) uniform screenUbo
{
    vec4 inverseResolution;
} screen;

void main()
{
    outColor = vec4(fxaa(textureColor, samplerColor, fragUV, screen.inverseResolution.xy), 1.0);
}