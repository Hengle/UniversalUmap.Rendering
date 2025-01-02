using System.Numerics;

namespace UniversalUmap.Rendering.Input;

public class Frustum
{
    public Plane[] FrustumPlanes { get; }
    public Frustum()
    {
        FrustumPlanes = new Plane[6];
    }

    public void Update(Matrix4x4 viewProjectionMatrix)
    {
        // Left plane
        FrustumPlanes[0].Normal = new Vector3(
            viewProjectionMatrix.M14 + viewProjectionMatrix.M11,
            viewProjectionMatrix.M24 + viewProjectionMatrix.M21,
            viewProjectionMatrix.M34 + viewProjectionMatrix.M31
        );
        FrustumPlanes[0].D = viewProjectionMatrix.M44 + viewProjectionMatrix.M41;

        // Right plane
        FrustumPlanes[1].Normal = new Vector3(
            viewProjectionMatrix.M14 - viewProjectionMatrix.M11,
            viewProjectionMatrix.M24 - viewProjectionMatrix.M21,
            viewProjectionMatrix.M34 - viewProjectionMatrix.M31
        );
        FrustumPlanes[1].D = viewProjectionMatrix.M44 - viewProjectionMatrix.M41;

        // Top plane
        FrustumPlanes[2].Normal = new Vector3(
            viewProjectionMatrix.M14 - viewProjectionMatrix.M12,
            viewProjectionMatrix.M24 - viewProjectionMatrix.M22,
            viewProjectionMatrix.M34 - viewProjectionMatrix.M32
        );
        FrustumPlanes[2].D = viewProjectionMatrix.M44 - viewProjectionMatrix.M42;

        // Bottom plane
        FrustumPlanes[3].Normal = new Vector3(
            viewProjectionMatrix.M14 + viewProjectionMatrix.M12,
            viewProjectionMatrix.M24 + viewProjectionMatrix.M22,
            viewProjectionMatrix.M34 + viewProjectionMatrix.M32
        );
        FrustumPlanes[3].D = viewProjectionMatrix.M44 + viewProjectionMatrix.M42;

        // Near plane
        FrustumPlanes[4].Normal = new Vector3(
            viewProjectionMatrix.M13,
            viewProjectionMatrix.M23,
            viewProjectionMatrix.M33
        );
        FrustumPlanes[4].D = viewProjectionMatrix.M43;

        // Far plane
        FrustumPlanes[5].Normal = new Vector3(
            viewProjectionMatrix.M14 - viewProjectionMatrix.M13,
            viewProjectionMatrix.M24 - viewProjectionMatrix.M23,
            viewProjectionMatrix.M34 - viewProjectionMatrix.M33
        );
        FrustumPlanes[5].D = viewProjectionMatrix.M44 - viewProjectionMatrix.M43;
    }

}