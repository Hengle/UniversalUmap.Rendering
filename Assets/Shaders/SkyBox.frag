#version 450

#include "Utils/ToneMapping.glsl"

layout(set = 0, binding = 1) uniform textureCube cubeTexture;
layout(set = 0, binding = 2) uniform sampler linearSampler;

layout(location = 0) in vec3 fragTexCoord;
layout(location = 0) out vec4 fragColor;

void main()
{
    fragColor = vec4(toSRGB(reinhard(texture(samplerCube(cubeTexture, linearSampler), fragTexCoord).rgb)), 1.0);
}
