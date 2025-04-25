#define FXAA_QUALITY_SUBPIX 0.75
#define FXAA_EDGE_THRESHOLD 0.166
#define FXAA_EDGE_THRESHOLD_MIN 0.0833

// FXAA 3.11 implementation
vec3 fxaa(texture2D tex, sampler smp, vec2 uv, vec2 invRes) {
    // Luma weights (sRGB)
    const vec3 lumaCoeffs = vec3(0.299, 0.587, 0.114);

    // Sample 3x3 neighborhood
    float lumaM  = dot(texture(sampler2D(tex, smp), uv).rgb, lumaCoeffs);
    float lumaN  = dot(texture(sampler2D(tex, smp), uv + vec2( 0.0, -1.0) * invRes).rgb, lumaCoeffs);
    float lumaS  = dot(texture(sampler2D(tex, smp), uv + vec2( 0.0,  1.0) * invRes).rgb, lumaCoeffs);
    float lumaE  = dot(texture(sampler2D(tex, smp), uv + vec2( 1.0,  0.0) * invRes).rgb, lumaCoeffs);
    float lumaW  = dot(texture(sampler2D(tex, smp), uv + vec2(-1.0,  0.0) * invRes).rgb, lumaCoeffs);

    float lumaNE = dot(texture(sampler2D(tex, smp), uv + vec2( 1.0, -1.0) * invRes).rgb, lumaCoeffs);
    float lumaSE = dot(texture(sampler2D(tex, smp), uv + vec2( 1.0,  1.0) * invRes).rgb, lumaCoeffs);
    float lumaNW = dot(texture(sampler2D(tex, smp), uv + vec2(-1.0, -1.0) * invRes).rgb, lumaCoeffs);
    float lumaSW = dot(texture(sampler2D(tex, smp), uv + vec2(-1.0,  1.0) * invRes).rgb, lumaCoeffs);

    // Compute local contrast
    float lumaMin = min(lumaM, min(min(min(lumaN, lumaS), min(lumaE, lumaW)), min(min(lumaNE, lumaNW), min(lumaSE, lumaSW))));
    float lumaMax = max(lumaM, max(max(max(lumaN, lumaS), max(lumaE, lumaW)), max(max(lumaNE, lumaNW), max(lumaSE, lumaSW))));
    float lumaRange = lumaMax - lumaMin;

    if (lumaRange < max(FXAA_EDGE_THRESHOLD_MIN, lumaMax * FXAA_EDGE_THRESHOLD))
    return texture(sampler2D(tex, smp), uv).rgb;

    // Edge direction estimation
    float dirX = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    float dirY =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));

    float dirReduce = max(
        (lumaN + lumaS + lumaE + lumaW) * 0.25 * 0.25,
        1.0 / 128.0
    );

    float rcpDirMin = 1.0 / (min(abs(dirX), abs(dirY)) + dirReduce);

    vec2 dir = clamp(vec2(dirX, dirY) * rcpDirMin,
                     vec2(-8.0), vec2(8.0)) * invRes;

    // Sample along edge direction
    vec3 rgbA = 0.5 * (
    texture(sampler2D(tex, smp), uv + dir * (1.0/3.0 - 0.5)).rgb +
    texture(sampler2D(tex, smp), uv + dir * (2.0/3.0 - 0.5)).rgb
    );

    vec3 rgbB = 0.5 * rgbA + 0.25 * (
    texture(sampler2D(tex, smp), uv + dir * (-0.5)).rgb +
    texture(sampler2D(tex, smp), uv + dir * (0.5)).rgb
    );

    // Subpixel aliasing removal
    float lumaB = dot(rgbB, lumaCoeffs);
    float lumaLocalAvg = 0.5 * (lumaMin + lumaMax);
    float subPixel = clamp(abs(lumaB - lumaLocalAvg) / lumaRange, 0.0, 1.0);

    subPixel = pow(subPixel, FXAA_QUALITY_SUBPIX);

    return mix(rgbB, rgbA, subPixel);
}