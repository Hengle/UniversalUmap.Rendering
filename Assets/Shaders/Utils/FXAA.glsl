#define FXAA_SPAN_MAX 8.0
#define FXAA_REDUCE_MUL   (1.0/FXAA_SPAN_MAX)
#define FXAA_REDUCE_MIN   (1.0/128.0)
#define FXAA_SUBPIX_SHIFT (1.0/4.0)

vec3 fxaa(texture2D textureColor, sampler textureSampler, vec2 fragUV, vec2 invRes) {
    vec4 uv = vec4(fragUV, fragUV + FXAA_SUBPIX_SHIFT * invRes);
    
    vec3 rgbNW = texture(sampler2D(textureColor, textureSampler), uv.zw).xyz;
    vec3 rgbNE = texture(sampler2D(textureColor, textureSampler), uv.zw + vec2(1,0)*invRes.xy).xyz;
    vec3 rgbSW = texture(sampler2D(textureColor, textureSampler), uv.zw + vec2(0,1)*invRes.xy).xyz;
    vec3 rgbSE = texture(sampler2D(textureColor, textureSampler), uv.zw + vec2(1,1)*invRes.xy).xyz;
    vec3 rgbM  = texture(sampler2D(textureColor, textureSampler), uv.xy).xyz;

    vec3 luma = vec3(0.299, 0.587, 0.114);
    float lumaNW = dot(rgbNW, luma);
    float lumaNE = dot(rgbNE, luma);
    float lumaSW = dot(rgbSW, luma);
    float lumaSE = dot(rgbSE, luma);
    float lumaM  = dot(rgbM,  luma);

    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));

    vec2 dir;
    dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    dir.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));

    float dirReduce = max(
    (lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * FXAA_REDUCE_MUL),
    FXAA_REDUCE_MIN);
    float rcpDirMin = 1.0/(min(abs(dir.x), abs(dir.y)) + dirReduce);

    dir = min(vec2( FXAA_SPAN_MAX,  FXAA_SPAN_MAX),
    max(vec2(-FXAA_SPAN_MAX, -FXAA_SPAN_MAX),
    dir * rcpDirMin)) * invRes.xy;

    vec3 rgbA = (1.0/2.0) * (
    texture(sampler2D(textureColor, textureSampler), uv.xy + dir * (1.0/3.0 - 0.5)).xyz +
    texture(sampler2D(textureColor, textureSampler), uv.xy + dir * (2.0/3.0 - 0.5)).xyz);
    vec3 rgbB = rgbA * (1.0/2.0) + (1.0/4.0) * (
    texture(sampler2D(textureColor, textureSampler), uv.xy + dir * (0.0/3.0 - 0.5)).xyz +
    texture(sampler2D(textureColor, textureSampler), uv.xy + dir * (3.0/3.0 - 0.5)).xyz);

    float lumaB = dot(rgbB, luma);

    if((lumaB < lumaMin) || (lumaB > lumaMax)) return rgbA;

    return rgbB;
}
