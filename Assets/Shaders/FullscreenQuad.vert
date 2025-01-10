#version 450

layout(location = 0) in vec2 in_position;

layout(location = 0) out vec2 pass_texture_coordinate;

void main()
{
    gl_Position = vec4(in_position.x, in_position.y, 0.0f, 1.0f);
    pass_texture_coordinate = vec2(in_position.x, -in_position.y) * 0.5 + 0.5;
}