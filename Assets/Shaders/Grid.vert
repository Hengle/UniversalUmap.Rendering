#version 450

layout(set = 0, binding = 0) uniform cameraUbo {
    mat4 projection;
    mat4 view;
    vec4 position;
} camera;

layout(location = 0) in vec3 inPosition;

layout(location = 0) out vec3 nearPoint;
layout(location = 1) out vec3 farPoint;

vec3 UnprojectPoint(float x, float y, float z, mat4 view, mat4 projection) {
    mat4 viewInv = inverse(view);
    mat4 projInv = inverse(projection);
    vec4 unprojectedPoint =  viewInv * projInv * vec4(x, y, z, 1.0);
    return unprojectedPoint.xyz / unprojectedPoint.w;
}

void main() {
    nearPoint = UnprojectPoint(inPosition.x, inPosition.y, 0.0, camera.view, camera.projection); // unprojecting on the near plane
    farPoint = UnprojectPoint(inPosition.x, inPosition.y, 1.0, camera.view, camera.projection); // unprojecting on the far plane
    
    gl_Position = vec4(inPosition, 1.0); // using directly the clipped coordinates
}