using MotionControllers.Bowling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace MotionControllers.Editor
{
    public static class CreateBowlingScene
    {
        [MenuItem("Tools/Motion Controllers/Create Bowling Prototype Scene")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            string generated = AssetDatabase.GenerateUniqueAssetPath("Assets/BowlingGenerated");
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(generated));
            Material laneMaterial = MaterialAsset(generated, "Lane", new Color(0.65f, 0.39f, 0.16f));
            Material gutterMaterial = MaterialAsset(generated, "Gutters", new Color(0.10f, 0.15f, 0.20f));
            Material ballMaterial = MaterialAsset(generated, "Ball", new Color(0.045f, 0.23f, 0.8f));
            Material pinMaterial = MaterialAsset(generated, "Pins", new Color(0.95f, 0.96f, 0.98f));
            Material stripeMaterial = MaterialAsset(generated, "PinStripes", new Color(0.85f, 0.045f, 0.035f));
            Material aimMaterial = MaterialAsset(generated, "Aim", new Color(0.10f, 0.95f, 0.8f), true);
            var lanePhysics = new PhysicsMaterial("Lane contact") { dynamicFriction = 0.12f, staticFriction = 0.16f, bounciness = 0.02f,
                frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Minimum };
            var pinPhysics = new PhysicsMaterial("Pin contact") { dynamicFriction = 0.25f, staticFriction = 0.3f, bounciness = 0.08f };
            AssetDatabase.CreateAsset(lanePhysics, generated + "/Lane.physicsMaterial");
            AssetDatabase.CreateAsset(pinPhysics, generated + "/Pins.physicsMaterial");

            var environment = new GameObject("Bowling Lane");
            Box("Lane", new Vector3(0, -0.15f, 10), new Vector3(2.4f, 0.3f, 22), laneMaterial, lanePhysics, environment.transform);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Gutter floor", new Vector3(side * 1.48f, -0.35f, 10), new Vector3(0.56f, 0.2f, 22), gutterMaterial, lanePhysics, environment.transform);
                Box("Outer boundary", new Vector3(side * 1.8f, 0.08f, 10), new Vector3(0.12f, 0.75f, 22), gutterMaterial, lanePhysics, environment.transform);
            }
            Box("Catch tray", new Vector3(0, -0.45f, 21.5f), new Vector3(3.6f, 0.3f, 2), gutterMaterial, lanePhysics, environment.transform);
            // A few lane strips and a foul line provide depth without textures or extra collisions.
            for (int i = -3; i <= 3; i++)
                Marker("Board seam", new Vector3(i * 0.3f, 0.002f, 10), new Vector3(0.009f, 0.003f, 22), gutterMaterial, environment.transform);
            Marker("Foul line", new Vector3(0, 0.004f, 1), new Vector3(2.4f, 0.006f, 0.04f), pinMaterial, environment.transform);

            var releasePoint = new GameObject("Ball Release Point").transform;
            releasePoint.position = new Vector3(0, 0.165f, 0.2f);
            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere); ballObject.name = "Bowling Ball";
            ballObject.transform.position = releasePoint.position; ballObject.transform.localScale = Vector3.one * 0.32f;
            ballObject.GetComponent<Renderer>().sharedMaterial = ballMaterial;
            ballObject.GetComponent<SphereCollider>().sharedMaterial = lanePhysics;
            var ballBody = ballObject.AddComponent<Rigidbody>(); ballBody.mass = 6f;
            ballBody.interpolation = RigidbodyInterpolation.Interpolate;
            ballBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            ballBody.maxAngularVelocity = 100; ballBody.linearDamping = 0.015f; ballBody.angularDamping = 0.03f;
            ballBody.solverIterations = 12; ballBody.solverVelocityIterations = 4;
            var ball = ballObject.AddComponent<BowlingBall>(); ball.releasePoint = releasePoint;
            // Visible contrasting patch makes ball rotation easy to see; no extra collider.
            var spot = GameObject.CreatePrimitive(PrimitiveType.Sphere); spot.name = "Ball rotation marker";
            Object.DestroyImmediate(spot.GetComponent<Collider>());
            spot.transform.SetParent(ballObject.transform, false);
            spot.transform.localPosition = new Vector3(0, 0.47f, 0); spot.transform.localScale = Vector3.one * 0.16f;
            spot.GetComponent<Renderer>().sharedMaterial = aimMaterial;

            var rack = new GameObject("Ten Pin Rack").AddComponent<BowlingPinRack>();
            rack.pins = new Rigidbody[10];
            Mesh pinMesh = PinMesh(); AssetDatabase.CreateAsset(pinMesh, generated + "/Pin.asset");
            int index = 0;
            for (int row = 0; row < 4; row++)
                for (int column = 0; column <= row; column++)
                {
                    var pin = new GameObject("Pin " + (index + 1)); pin.transform.SetParent(rack.transform);
                    pin.transform.position = new Vector3((column - row * 0.5f) * 0.36f, 0.008f, 17.5f + row * 0.312f);
                    pin.AddComponent<MeshFilter>().sharedMesh = pinMesh;
                    pin.AddComponent<MeshRenderer>().sharedMaterials = new[] { pinMaterial, stripeMaterial };
                    var foot = pin.AddComponent<BoxCollider>(); foot.center = new Vector3(0, 0.025f, 0); foot.size = new Vector3(0.14f, 0.05f, 0.14f); foot.sharedMaterial = pinPhysics;
                    var body = pin.AddComponent<CapsuleCollider>(); body.center = new Vector3(0, 0.15f, 0); body.radius = 0.105f; body.height = 0.26f; body.sharedMaterial = pinPhysics;
                    var head = pin.AddComponent<CapsuleCollider>(); head.center = new Vector3(0, 0.345f, 0); head.radius = 0.058f; head.height = 0.2f; head.sharedMaterial = pinPhysics;
                    var rigidbody = pin.AddComponent<Rigidbody>(); rigidbody.mass = 1.5f;
                    rigidbody.centerOfMass = new Vector3(0, 0.16f, 0); rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                    rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rigidbody.solverIterations = 12; rigidbody.solverVelocityIterations = 4;
                    rigidbody.maxAngularVelocity = 40; rigidbody.angularDamping = 0.05f;
                    rack.pins[index++] = rigidbody;
                }
            var aim = new GameObject("Aim Indicator").AddComponent<LineRenderer>();
            aim.positionCount = 2; aim.useWorldSpace = true; aim.startWidth = 0.025f; aim.endWidth = 0.07f;
            aim.sharedMaterial = aimMaterial; aim.SetPositions(new[] { new Vector3(0, 0.025f, 0.2f), new Vector3(0, 0.025f, 16.2f) });

            var system = new GameObject("Controller System");
            var manager = system.AddComponent<ControllerManager>();
            system.AddComponent<ControllerReceiver>();
            var debug = system.AddComponent<ControllerDebugPanel>();
            var bowling = system.AddComponent<BowlingThrowController>();
            bowling.inputSource = manager; bowling.ball = ball; bowling.pinRack = rack; bowling.aimIndicator = aim;
            debug.gameDebugSource = bowling;

            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 8, -7);
            camera.transform.LookAt(new Vector3(0, 0, 10)); camera.fieldOfView = 52;
            camera.rect = new Rect(0.42f, 0, 0.58f, 1);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.035f, 0.055f, 0.085f);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Directional Light").AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 2; light.transform.rotation = Quaternion.Euler(45, -25, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BowlingPrototype.unity"));
            Selection.activeGameObject = system;
        }

        private static Material MaterialAsset(string folder, string name, Color color, bool unlit = false)
        {
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find(unlit ? "Unlit/Color" : "Standard");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(material, folder + "/" + name + ".mat"); return material;
        }
        private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material, PhysicsMaterial physics, Transform parent)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
            box.transform.SetParent(parent); box.transform.position = position; box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material; box.GetComponent<Collider>().sharedMaterial = physics; return box;
        }
        private static void Marker(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        { var marker = Box(name, position, scale, material, null, parent); Object.DestroyImmediate(marker.GetComponent<Collider>()); }
        private static Mesh PinMesh()
        {
            // Lathed silhouette with two red neck bands; compound colliders remain inexpensive.
            var profile = new[] { new Vector2(0.07f,0), new Vector2(0.1f,0.035f), new Vector2(0.12f,0.12f),
                new Vector2(0.10f,0.20f), new Vector2(0.046f,0.27f), new Vector2(0.044f,0.285f),
                new Vector2(0.043f,0.30f), new Vector2(0.045f,0.315f), new Vector2(0.048f,0.33f),
                new Vector2(0.065f,0.375f), new Vector2(0.05f,0.425f), new Vector2(0,0.45f) };
            const int segments = 24;
            var vertices = new List<Vector3>(); var white = new List<int>(); var red = new List<int>();
            foreach (var ring in profile)
                for (int j = 0; j <= segments; j++)
                { float angle = j * Mathf.PI * 2 / segments; vertices.Add(new Vector3(Mathf.Cos(angle) * ring.x, ring.y, Mathf.Sin(angle) * ring.x)); }
            for (int i = 0; i < profile.Length - 1; i++)
                for (int j = 0; j < segments; j++)
                {
                    int a = i * (segments + 1) + j, b = a + segments + 1;
                    var triangles = i == 4 || i == 6 ? red : white;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            int center = vertices.Count; vertices.Add(Vector3.zero);
            for (int j = 0; j < segments; j++) { white.Add(center); white.Add(j); white.Add(j + 1); }
            var mesh = new Mesh { name = "Bowling pin" }; mesh.SetVertices(vertices); mesh.subMeshCount = 2;
            mesh.SetTriangles(white, 0); mesh.SetTriangles(red, 1); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
