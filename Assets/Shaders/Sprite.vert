#version 450

layout(set = 0, binding = 0) uniform cameraUbo
{
    mat4 projection;
    mat4 view;
    vec4 position;
} camera;

layout(set = 0, binding = 1) uniform ScaleUbo
{
    float scale;
};

layout(location = 0) in vec2 inPosition;

layout(location = 2) in vec4 matrixRow0;
layout(location = 3) in vec4 matrixRow1;
layout(location = 4) in vec4 matrixRow2;
layout(location = 5) in vec4 matrixRow3;

layout(location = 0) out vec2 fragUV;

void main()
{
    // Camera's right and up vectors in world space
    vec3 cameraRightWorldspace = vec3(camera.view[0][0], camera.view[1][0], camera.view[2][0]);
    vec3 cameraUpWorldspace = vec3(camera.view[0][1], camera.view[1][1], camera.view[2][1]);

    // Object transformation matrix
    mat4 modelMatrix = mat4(matrixRow0, matrixRow1, matrixRow2, matrixRow3);

    // Vertex position in object space
    vec4 vertexPositionWorldspace = modelMatrix * vec4(0.0, 0.0, 0.0, 1.0);

    // Apply scale and use inPosition as offsets
    vertexPositionWorldspace.xyz += cameraRightWorldspace * (inPosition.x * scale);
    vertexPositionWorldspace.xyz += cameraUpWorldspace * (inPosition.y * scale);

    // Project the final position
    gl_Position = camera.projection * vertexPositionWorldspace;

    // Pass the inPosition as UV to the fragment shader
    fragUV = inPosition;
}
