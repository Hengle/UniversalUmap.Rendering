#version 450

layout(set = 0, binding = 0) uniform cameraUbo {
    mat4 projection;
    mat4 view;
    vec4 position;
} camera;

const float near = 10.0;
const float far = 1000000.0;

layout(location = 0) in vec3 nearPoint;
layout(location = 1) in vec3 farPoint;

layout(location = 0) out vec4 outColor;


float computeDepth(vec3 pos) {
    vec4 clip_space_pos = camera.projection * camera.view * vec4(pos.xyz, 1.0);
    return (clip_space_pos.z / clip_space_pos.w);
}

float computeLinearDepth(vec3 pos) {
    vec4 clip_space_pos = camera.projection * camera.view * vec4(pos.xyz, 1.0);
    float clip_space_depth = (clip_space_pos.z / clip_space_pos.w) * 2.0 - 1.0;
    float linearDepth = (2.0 * near * far) / (far + near - clip_space_depth * (far - near));
    return linearDepth / far;
}

float pristineGrid(in vec2 uv, float lineWidth) {
    vec2 dx = dFdx(uv);
    vec2 dy = dFdy(uv);
    vec2 uvDeriv = vec2(length(vec2(dx.x, dy.x)), length(vec2(dx.y, dy.y)));
    bvec2 invertLine = bvec2(lineWidth > 0.5, lineWidth > 0.5);
    vec2 targetWidth = vec2(
    invertLine.x ? 1.0 - lineWidth : lineWidth,
    invertLine.y ? 1.0 - lineWidth : lineWidth
    );
    vec2 drawWidth = clamp(targetWidth, uvDeriv, vec2(0.5));
    vec2 lineAA = uvDeriv * 1.5;
    vec2 gridUV = abs(fract(uv) * 2.0 - 1.0);
    gridUV.x = invertLine.x ? gridUV.x : 1.0 - gridUV.x;
    gridUV.y = invertLine.y ? gridUV.y : 1.0 - gridUV.y;
    vec2 grid2 = smoothstep(drawWidth + lineAA, drawWidth - lineAA, gridUV);
    grid2 *= clamp(targetWidth / drawWidth, 0.0, 1.0);
    grid2 = mix(grid2, targetWidth, clamp(uvDeriv * 2.0 - 1.0, 0.0, 1.0));
    grid2.x = invertLine.x ? 1.0 - grid2.x : grid2.x;
    grid2.y = invertLine.y ? 1.0 - grid2.y : grid2.y;
    return mix(grid2.x, 1.0, grid2.y);
}

vec4 grid(vec3 fragPos3D, float scale, float lineWidth) {
    scale = 1 / scale;
    
    vec2 coord = fragPos3D.xz * scale;
    
    float gridEffect = pristineGrid(coord, lineWidth);

    vec4 color = vec4(0.2, 0.2, 0.2, gridEffect);

    float axisThreshold = (lineWidth / scale * 0.5);
    
    // x-axis
    if(fragPos3D.x > -axisThreshold && fragPos3D.x < axisThreshold)
        color.g = 1.0;

    // z-axis
    if(fragPos3D.z > -axisThreshold && fragPos3D.z < axisThreshold)
        color.r = 1.0;

    return color;
}

const float sphereRadius = 1000000000.0;

const float lineWidth = 0.02;

const float scaleSmall = 10;
const float scaleMedium = 100;
const float scaleLarge = 1000;

const float blendDistanceSmall = 1000;
const float blendDistanceMedium = 5000;

const float blendRadiusSmall = 10000.0;
const float blendRadiusMedium = 10000.0;

void main() {
    float t = -nearPoint.y / (farPoint.y - nearPoint.y);
    vec3 fragPos3D = nearPoint + t * (farPoint - nearPoint.y);
    gl_FragDepth = computeDepth(fragPos3D);
    
    float distanceToCenter = length(fragPos3D - camera.position.xyz);
    float sphereMask = 1 - clamp(distanceToCenter / sphereRadius, 0.0, 1.0);
    
    vec4 gridSmall = grid(fragPos3D, scaleSmall, lineWidth);
    vec4 gridMedium = grid(fragPos3D, scaleMedium, lineWidth);
    vec4 gridLarge = grid(fragPos3D, scaleLarge, lineWidth);
    
    float fadeSmall = clamp(1 - (distanceToCenter - blendDistanceSmall) / blendRadiusSmall, 0.0, 1.0);
    float fadeMedium = clamp(1 - (distanceToCenter - blendDistanceMedium) / blendRadiusMedium, 0.0, 1.0);

    gridSmall.a *= fadeSmall * 0.5;
    gridMedium.a *= fadeMedium * 0.75;
    
    outColor = gridSmall;
    
    outColor = mix(outColor, gridMedium, gridMedium.a);
    
    outColor = mix(outColor, gridLarge, gridLarge.a);
    
    outColor.a *= sphereMask * float(t > 0.0);
}



