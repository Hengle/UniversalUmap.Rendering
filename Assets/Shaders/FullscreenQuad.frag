#version 450

layout(set = 0, binding = 0) uniform texture2D textureColor;
layout(set = 0, binding = 1) uniform sampler samplerColor;

layout(location = 0) in vec2 fragUV;
layout(location = 0) out vec4 outColor;

void main()
{
    outColor = vec4(texture(sampler2D(textureColor, samplerColor), fragUV).rgb, 1.0);
}
