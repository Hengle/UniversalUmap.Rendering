#version 450

#include "Utils/ToneMapping.glsl"

layout(location = 0) in vec3 fragPosition;
layout(location = 1) in vec4 fragColor;
layout(location = 2) in vec3 fragNormal;
layout(location = 3) in vec3 fragTangent;
layout(location = 4) in vec2 fragUV;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform autoTextureUbo
{
    vec4 colorMask;
    vec4 metallicMask;
    vec4 specularMask;
    vec4 roughnessMask;
    vec4 aoMask;
    vec4 normalMask;
    vec4 emissiveMask;
    vec4 alphaMask;
} autoTexture;

layout(set = 1, binding = 0) uniform cameraUbo
{
    mat4 projection;
    mat4 view;
    vec4 position;
} camera;

layout(set = 2, binding = 0) uniform textureCube irradianceTextureCube;
layout(set = 2, binding = 1) uniform textureCube radianceTextureCube;
layout(set = 2, binding = 3) uniform texture2D brdfLutTexture;
layout(set = 2, binding = 4) uniform sampler aniso4xSampler;

layout(set = 3, binding = 0) uniform texture2D colorTexture;
layout(set = 3, binding = 1) uniform texture2D metallicTexture;
layout(set = 3, binding = 2) uniform texture2D specularTexture;
layout(set = 3, binding = 3) uniform texture2D roughnessTexture;
layout(set = 3, binding = 4) uniform texture2D aoTexture;
layout(set = 3, binding = 5) uniform texture2D normalTexture;
layout(set = 3, binding = 6) uniform texture2D alphaTexture;
layout(set = 3, binding = 7) uniform texture2D emissiveTexture;

const float PI = 3.14159265359;
const float MAX_REFLECTION_LOD = 6.0;
const vec3 directionalLightVector = normalize(vec3(1.0, 1.0, 0.0)); // Directional light (sun)

float getMaskedChannel(vec4 textureSample, vec4 mask) {
    float greyscale = dot(textureSample.rgb, vec3(1.0 / 3.0));
    return dot(textureSample, mask) / max(dot(mask.rgb, vec3(1.0)), 0.001);  //Avoid division by zero
}

// ----------------------------------------------------------------------------
float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness*roughness;
    float a2 = a*a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH*NdotH;

    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}
// ----------------------------------------------------------------------------
float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r*r) / 8.0;

    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}
// ----------------------------------------------------------------------------
float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}
// ----------------------------------------------------------------------------
vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}
// ----------------------------------------------------------------------------

vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness)
{
    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 CookTorranceBRDF(vec3 F0, vec3 N, vec3 V, vec3 L, vec3 albedo, float metallic, float roughness, float specular)
{
    vec3 H = normalize(V + L);  // Halfway vector
    
    // Cook-Torrance BRDF
    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001; // + 0.0001 to prevent divide by zero
    vec3 nominatorByDenominator = numerator / denominator;

    // kS is equal to Fresnel
    vec3 kS = F;
    // for energy conservation, the diffuse and specular light can't
    // be above 1.0 (unless the surface emits light); to preserve this
    // relationship the diffuse component (kD) should equal 1.0 - kS.
    vec3 kD = vec3(1.0) - kS;
    // multiply kD by the inverse metalness such that only non-metals 
    // have diffuse lighting, or a linear blend if partly metal (pure metals
    // have no diffuse light).
    kD *= 1.0 - metallic;

    float NdotL = max(dot(N, L), 0.0);
    
    return (kD * albedo / PI + nominatorByDenominator) * NdotL;
}

vec3 calculateDirectionalLight(float intensity, vec3 F0, vec3 normalVector, vec3 viewVector, vec3 albedo, float metallic, float roughness, float specular)
{
    return intensity * CookTorranceBRDF(F0, normalVector, viewVector, directionalLightVector, albedo, metallic, roughness, specular);
}

vec3 calculateSkyLight(vec3 F0, vec3 normalVector, vec3 viewVector, vec3 albedoTexture, float metallicTexture, float roughnessTexture, float specularTexture, float aoTexture)
{
    float NdotV = max(dot(normalVector, viewVector), 0.0);

    vec3 F = fresnelSchlickRoughness(NdotV, F0, roughnessTexture);
    vec3 kS = F;
    vec3 kD = 1.0 - kS;
    kD *= 1.0 - metallicTexture;

    vec3 irradiance = texture(samplerCube(irradianceTextureCube, aniso4xSampler), normalVector).rgb;
    vec3 diffuse = irradiance * albedoTexture;

    vec3 reflectVector = reflect(-viewVector, normalVector);
    vec3 radiance = textureLod(samplerCube(radianceTextureCube, aniso4xSampler), reflectVector, roughnessTexture * MAX_REFLECTION_LOD).rgb;
    
    vec2 envBRDF = textureLod(sampler2D(brdfLutTexture, aniso4xSampler), vec2(NdotV, roughnessTexture), 0.0).rg;
    vec3 specular = radiance * F * (envBRDF.r + envBRDF.g) * (specularTexture * 2.0);

    return (kD * diffuse + specular) * aoTexture;
}

vec3 calculatePointLight(float intensity, vec3 F0, vec3 normalVector, vec3 viewVector, vec3 lightVector, vec3 albedo, float metallic, float roughness, float specular)
{
    float distance = length(lightVector);
    intensity = intensity / (distance * distance);  //Inverse squared falloff
    return intensity * CookTorranceBRDF(F0, normalVector, viewVector, lightVector, albedo, metallic, roughness, specular);
}

vec3 calculateNormals(vec3 fragNormal, vec3 fragTangent, vec3 normalTexture)
{
    normalTexture = normalTexture * 2.0 - 1.0;
    normalTexture.y = -normalTexture.y;
    normalTexture = normalize(normalTexture);
    
    vec3 t  = normalize(fragTangent);
    vec3 n  = normalize(fragNormal);
    vec3 b  = normalize(cross(n, t));
    mat3 tbn = mat3(t, b, n);

    return normalize(tbn * normalTexture);
}

void main()
{
    vec3 albedo = (texture(sampler2D(colorTexture, aniso4xSampler), fragUV) * autoTexture.colorMask).rgb;
    float metallic = getMaskedChannel(texture(sampler2D(metallicTexture, aniso4xSampler), fragUV), autoTexture.metallicMask);
    float roughness = getMaskedChannel(texture(sampler2D(roughnessTexture, aniso4xSampler), fragUV), autoTexture.roughnessMask);
    float specular = getMaskedChannel(texture(sampler2D(specularTexture, aniso4xSampler), fragUV), autoTexture.specularMask);
    float ao = getMaskedChannel(texture(sampler2D(aoTexture, aniso4xSampler), fragUV), autoTexture.aoMask);
    vec3 normal = (texture(sampler2D(normalTexture, aniso4xSampler), fragUV) * autoTexture.normalMask).rgb;
    
    vec3 normalVector = calculateNormals(fragNormal, fragTangent, normal);

    vec3 F0 = vec3(0.04);  //Base reflectance for dielectric
    F0 = mix(F0, albedo, metallic);  //For metallics, mix F0 with albedo

    vec3 viewVector = normalize(camera.position.xyz - fragPosition);
    
    vec3 skyLight = calculateSkyLight(F0, normalVector, viewVector, albedo, metallic, roughness, specular, ao);
    
    vec3 pointLight = calculatePointLight(5.0, F0, normalVector, viewVector, viewVector, albedo, metallic, roughness, specular);
    
    outColor = vec4(toSRGB(agx(skyLight + pointLight)), 1.0);
}