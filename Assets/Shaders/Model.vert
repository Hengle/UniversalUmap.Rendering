#version 450

layout(location = 0) in vec3 position;
layout(location = 1) in vec4 color;
layout(location = 2) in vec3 normal;
layout(location = 3) in vec3 tangent;
layout(location = 4) in vec2 uv;

layout(location = 5) in vec4 matrixRow0;
layout(location = 6) in vec4 matrixRow1;
layout(location = 7) in vec4 matrixRow2;
layout(location = 8) in vec4 matrixRow3;

layout(location = 0) out vec3 fragPosition;
layout(location = 1) out vec4 fragColor;
layout(location = 2) out vec3 fragNormal;
layout(location = 3) out vec3 fragTangent;
layout(location = 4) out vec2 fragUV;

layout(set = 0, binding = 0) uniform cameraUbo
{
    mat4 projection;
    mat4 view;
    vec4 position;
} camera;

void main() {
    //instance transformation matrix
    mat4 instanceTransform = mat4(matrixRow0, matrixRow1, matrixRow2, matrixRow3);
    
    //vertex world position 
    vec4 worldPosition = instanceTransform * vec4(position, 1.0);
    
    //position in clip space (projection * view * world position)
    gl_Position = camera.projection * camera.view * worldPosition;
    
    //invert and transpose for normal matrix
    mat3 normalWorldMatrix = transpose(inverse(mat3(instanceTransform)));

    //normal and tangent to world space
    fragNormal = normalize(normalWorldMatrix * normal);
    fragTangent = normalize(normalWorldMatrix * tangent);

    //pass
    fragPosition = worldPosition.xyz;
    fragColor = color;
    fragUV = uv;
}
