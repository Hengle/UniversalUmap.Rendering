using System.Numerics;
using UniversalUmap.Rendering.Input;
using UniversalUmap.Rendering.Resources;
using Veldrid;

namespace UniversalUmap.Rendering.Camera;

public class Camera : IDisposable
{
    private readonly GraphicsDevice GraphicsDevice;
    public readonly DeviceBuffer CameraBuffer;
    public readonly ResourceLayout CameraResourceLayout;
    public readonly ResourceSet CameraResourceSet;

    public readonly Frustum Frustum;
    
    private readonly Vector3 Up;
    
    private Vector3 Position;
    private Vector3 Direction;
    
    private readonly float Far;
    private readonly float Near;
    private readonly float MouseSpeed;
    private readonly float FlySpeed;
    
    private float Fov;
    private float AspectRatio;
    
    private Matrix4x4 ProjectionMatrix;

    public Vector3 JumpPosition { get; set; }

    public Camera(GraphicsDevice graphicsDevice, Vector3 position, Vector3 direction, float aspectRatio)
    {
        GraphicsDevice = graphicsDevice;
        Frustum = new Frustum();
        
        Position = position;
        JumpPosition = position;
        Direction = direction;
        AspectRatio = aspectRatio;
        Fov = 90f;
        Far = 1000000f;
        Near = 10f;
        MouseSpeed = 1f;
        FlySpeed = 1f;
        Up = Vector3.UnitY;
        
        ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(Fov * (float)Math.PI / 180f, AspectRatio, Near, Far);
        
        //camera resources
        CameraResourceLayout = GraphicsDevice.ResourceFactory.CreateResourceLayout(
            new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("cameraUbo", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment)
            )
        );
        CameraBuffer = GraphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription(CameraUniform.SizeOf(), BufferUsage.UniformBuffer));
        CameraResourceSet = GraphicsDevice.ResourceFactory.CreateResourceSet(new ResourceSetDescription(CameraResourceLayout, CameraBuffer));
    }
    
    private void UpdateBuffer()
    {
        // Update camera buffer
        var viewMatrix = Matrix4x4.CreateLookAt(Position, Direction, Vector3.UnitY);
        GraphicsDevice.UpdateBuffer(CameraBuffer, 0, new CameraUniform(ProjectionMatrix, viewMatrix, Position));
        
        // Update frustum
        Frustum.Update(viewMatrix * ProjectionMatrix);
    }

    private void UpdateProjectionMatrix()
    {
        ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(Fov * (float)Math.PI / 180f, AspectRatio, Near, Far);
    }
    
    public void Update(double deltaTime)
    {
        if(!InputTracker.GetMouseButton(MouseButton.Right))
            return;
        
        //Mouse
        var inverseLookAtVector = Direction - Position;
        var mouseDelta = InputTracker.MouseDelta * MouseSpeed * 0.01f;
        var right = Vector3.Normalize(Vector3.Cross(inverseLookAtVector, Up));
        //Combine rotations
        var rotation = Matrix4x4.CreateFromAxisAngle(right, -mouseDelta.Y) * Matrix4x4.CreateFromAxisAngle(-Up, mouseDelta.X);
        
        //Keyboard
        var lookAtVector = Position - Direction;
        var moveAxis = Vector3.Normalize(-lookAtVector);
        var panAxis = Vector3.Normalize(Vector3.Cross(moveAxis, Up));
        
        var multiplier = InputTracker.GetKey(Key.ShiftLeft) ? 8000f : 500f * FlySpeed;
        var moveSpeed = (float)(multiplier * deltaTime);
        
        Direction = Vector3.Transform(inverseLookAtVector, rotation) + Position;
        if (InputTracker.GetKey(Key.W)) // forward
        {
            var d = moveSpeed * moveAxis;
            Position += d;
            Direction += d;
        }
        if (InputTracker.GetKey(Key.S)) // backward
        {
            var d = moveSpeed * moveAxis;
            Position -= d;
            Direction -= d;
        }
        if (InputTracker.GetKey(Key.A)) // left
        {
            var d = panAxis * moveSpeed;
            Position -= d;
            Direction -= d;
        }
        if (InputTracker.GetKey(Key.D)) // right
        {
            var d = panAxis * moveSpeed;
            Position += d;
            Direction += d;
        }
        if (InputTracker.GetKey(Key.Q)) // down
        {
            var d = moveSpeed * Up;
            Position -= d;
            Direction -= d;
        }
        if (InputTracker.GetKey(Key.E)) // up
        {
            var d = moveSpeed * Up;
            Position += d;
            Direction += d;
        }

        if (InputTracker.GetKey(Key.C)) // zoom in
            Zoom(+50, deltaTime);
        if (InputTracker.GetKey(Key.X)) // zoom out
            Zoom(-50, deltaTime);

        if (InputTracker.GetKeyDown(Key.F))
            JumpToPosition();

        UpdateBuffer();
    }

    private void JumpToPosition()
    {
        Position = JumpPosition;
    }

    private void Zoom(double amount, double deltaTime)
    {
        Fov = (float)Math.Clamp(Fov - (amount * deltaTime), 25d, 120d);
        UpdateProjectionMatrix();
        UpdateBuffer();
    }

    public void Resize(uint width, uint height)
    {
        AspectRatio = (float)width/height;
        UpdateProjectionMatrix();
        UpdateBuffer();
    }

    public void Dispose()
    {
        CameraResourceLayout.Dispose();
        CameraResourceSet.Dispose();
        CameraBuffer.Dispose();
    }
}

