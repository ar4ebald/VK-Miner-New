using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Windows;
using GraphSharp;
using GraphSharp.Algorithms.Layout.Simple.FDP;
using GraphSharp.Algorithms.Layout.Simple.Tree;
using GraphSharp.Algorithms.OverlapRemoval;
using Newtonsoft.Json.Linq;
using QuickGraph;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using SharpDX.Toolkit.Input;
using Buffer = SharpDX.Toolkit.Graphics.Buffer;

using NodeType = SharpDX_Test.User;
using EdgeType = QuickGraph.Edge<SharpDX_Test.User>;
using GraphType = QuickGraph.BidirectionalGraph<SharpDX_Test.User, QuickGraph.Edge<SharpDX_Test.User>>;
using Point = System.Windows.Point;

namespace SharpDX_Test
{

    class Program : Game
    {
        private GraphicsDeviceManager graphicsDeviceManager;
        private KeyboardManager KeyboardManager;

        private Effect Shader;
        private EffectPass DrawNodesPass;
        private EffectPass DrawEdgesPass;
        private EffectPass UpdateNodesPass;

        private User[] _users;
        private Dictionary<int, User> ById;

        private Texture2D PhotoAtlas;
        private Buffer<NodeVertex> NodesBuffer;
        private Buffer<NodeVertex> NewNodesBuffer;
        private Buffer<EdgeVertex> EdgesBuffer;
        private Buffer<int> EdgeIndicesBuffer;

        private const int BlockSize = 128;

        private float _scale, _rotation;
        private Vector2 _position;

        private Vector2 Scale2 => new Vector2(_scale * GraphicsDevice.BackBuffer.Height / GraphicsDevice.BackBuffer.Width, _scale);
        private Stopwatch drawTimer = Stopwatch.StartNew();

        private ControlPanelForm controlPanel;

        [Range(0, 100)]
        public float RepulsionForce = 2 / 60f;
        [Range(0, 10)]
        public float RepulsionDistance = 6f;
        [Range(0, 10)]
        public float RepulsionMax = 0.1f;
        [Range(0, 5)]
        public float RepulsionPower = 2;
        [Range(0, 10)]
        public float AttractionForce = 2 / 60f;
        [Range(0, 10)]
        public float AttractionDistance = 5;
        [Range(0, 5)]
        public float AttractionPower = 1;
        [Range(0, 4)]
        public float Dumping = 2f / 60f;
        [Range(0, 1)]
        public float AccelerationMinSquare = 0.01f;

        public string[] PropNames = {
            nameof(RepulsionForce), nameof(RepulsionDistance), nameof(RepulsionMax), nameof(RepulsionPower),
            nameof(AttractionForce), nameof(AttractionDistance), nameof(AttractionPower),
            nameof(Dumping), nameof(AccelerationMinSquare)
        };

        private Program()
        {
            graphicsDeviceManager = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1280,
                PreferredBackBufferHeight = 720
            };
            KeyboardManager = new KeyboardManager(this);
            Content.RootDirectory = Environment.CurrentDirectory;

            controlPanel = new ControlPanelForm(this, PropNames);
            controlPanel.Show();
        }

        protected override void Initialize()
        {
            Window.IsMouseVisible = true;
            Shader = Content.Load<Effect>("Shader");

            DrawNodesPass = Shader.CurrentTechnique.Passes["DrawNodes"];
            DrawEdgesPass = Shader.CurrentTechnique.Passes["DrawEdges"];
            UpdateNodesPass = Shader.CurrentTechnique.Passes["UpdateNodes"];

            PhotoAtlas = Texture2D.New(GraphicsDevice, 100, 100, PixelFormat.R8G8B8A8.UNorm, arraySize: 2048);

            ReadUsers();

            var edges = new List<EdgeVertex>();
            var edgeIndices = new List<int>();
            var vertices = new NodeVertex[_users.Length];


            var graph = new BidirectionalGraph<NodeType, EdgeType>();
            graph.AddVertexRange(_users);
            graph.AddEdgeRange(_users.SelectMany(i => i.FriendsSet.Select(j => new EdgeType(i, j))));
            var points = new Dictionary<NodeType, Point>();
            var alg = new GraphSharp.Algorithms.Layout.Simple.FDP.ISOMLayoutAlgorithm<NodeType, EdgeType, GraphType>(graph, points, new ISOMLayoutParameters()
            {
                Width = 128,
                Height = 78
            });

            for (var i = 0; i < _users.Length; i++)
            {
                var user = _users[i];
                var angle = 3.2 * Math.Sqrt(i + 1);
                var position = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * (float)angle * 0.25f;
                points.Add(user, new Point(position.X, position.Y));
            }
            alg.Compute();

            var positions = new Vector2[_users.Length];
            foreach (var point in alg.VertexPositions)
                positions[point.Key.ArrayIndex] = new Vector2((float)point.Value.X, (float)point.Value.Y);

            float dt = 1 / 60f;
            for (var iterCount = 10; iterCount != 0; iterCount--)
            {
                for (var i = 0; i < positions.Length; i++)
                    for (var j = i + 1; j < positions.Length; j++)
                    {
                        var delta = positions[j] - positions[i];
                        var distance = delta.Length();
                        delta /= distance;

                        var force = dt * delta * (RepulsionForce / ((float)Math.Pow(distance / RepulsionDistance, RepulsionPower) + RepulsionMax));
                        positions[i] -= force;
                        positions[j] += force;
                    }
            }

            for (var i = 0; i < _users.Length; i++)
            {
                var user = _users[i];

                //var position = positions[i];

                //var point = alg.VertexPositions[user];
                //var position = new Vector2((float)point.X, (float)point.Y);

                var angle = 3.2 * Math.Sqrt(i + 1);
                var position = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * (float)angle * 0.25f;

                //var angle = MathUtil.TwoPi * i / _users.Length;
                //var r = 2 * _users.Length / MathUtil.TwoPi;
                //var position = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * r;

                user.Position = position;
                vertices[i] = new NodeVertex(position, i, edgeIndices.Count, user.AllFriends.Length);
                edgeIndices.AddRange(user.AllFriends.Select(j => j.ArrayIndex));
                edges.AddRange(user.Friends.Select(j => new EdgeVertex(i, j.ArrayIndex)));
            }

            NodesBuffer = Buffer.Structured.New(GraphicsDevice, vertices, true);
            NewNodesBuffer = Buffer.Structured.New(GraphicsDevice, vertices, true);
            EdgesBuffer = Buffer.Structured.New(GraphicsDevice, edges.ToArray());
            EdgeIndicesBuffer = Buffer.Structured.New(GraphicsDevice, edgeIndices.ToArray());

            _scale = 1 / vertices.Last().Position.Length();
            _position = Vector2.Zero;
            _rotation = 0;

            Shader.Parameters["Edges"].SetResource(EdgesBuffer);
            Shader.Parameters["EdgeIndices"].SetResource(EdgeIndicesBuffer);
            Shader.Parameters["atlas"].SetResource(PhotoAtlas);
            Shader.Parameters["atlasSampler"].SetResource(GraphicsDevice.SamplerStates.LinearWrap);
            //Shader.Parameters["scale"].SetValue(Scale2);
            //Shader.Parameters["camPos"].SetValue(_position);

            Layout();
        }

        private void ReadUsers()
        {
            ById = new Dictionary<int, User>();
            var json = JObject.Parse(File.ReadAllText("Users.json"));
            _users = json["Users"].Select(i =>
                new User
                {
                    Id = i.Value<int>("Id"),
                    Name = i.Value<string>("Name"),
                    Photo = i.Value<string>("Photo")
                }).ToArray();
            for (var i = 0; i < _users.Length; i++)
                _users[i].ArrayIndex = i;
            ById = _users.ToDictionary(i => i.Id);

            foreach (var edge in json["Friends"].Select(i => new { Id = i.Value<int>("Id"), Friends = i["Friends"].Select(j => j.Value<int>()).ToArray() }))
            {
                ById[edge.Id].AllFriendsSet = new HashSet<User>(edge.Friends.Where(i => ById.ContainsKey(i)).Select(i => ById[i]));
                ById[edge.Id].FriendsSet = new HashSet<User>();
            }

            foreach (var user in _users)
            {
                foreach (var friend in user.AllFriendsSet.Where(friend => !friend.AllFriendsSet.Contains(user)))
                    friend.AllFriendsSet.Add(user);
                foreach (var friend in user.AllFriendsSet.Where(friend => !friend.FriendsSet.Contains(user)))
                    user.FriendsSet.Add(friend);
            }

            foreach (var user in _users)
            {
                user.AllFriends = user.AllFriendsSet.ToArray();
                user.Friends = user.FriendsSet.ToArray();
            }

            foreach (var file in Directory.GetFiles("Photos"))
            {
                var id = int.Parse(Regex.Match(file, @"(?<=Photos\\)\d+(?=_)").Value);
                using (var buffer = Texture2D.Load(GraphicsDevice, file))
                {
                    int index = 0;
                    if (ById.ContainsKey(id))
                        index = ById[id].ArrayIndex;

                    GraphicsDevice.Copy(buffer, 0, PhotoAtlas, index);
                }
            }
        }

        private Stopwatch timer = Stopwatch.StartNew();

        private void Layout()
        {
            Shader.Parameters["scale"].SetValue(Scale2);
            //Shader.Parameters["camPos"].SetValue(_position);
            Shader.Parameters["EdgeBorderColor"].SetValue(new Color3(0.5f, 0.5f, 0.5f));
            Shader.Parameters["EdgeCenterColor"].SetValue(new Color3(0.8f, 0.8f, 0.8f));
            Shader.Parameters["pixelWidth"].SetValue(1f / GraphicsDevice.BackBuffer.Width);

            foreach (var name in PropNames)
                Shader.Parameters[name].SetValue((float)typeof(Program).GetField(name).GetValue(this));

            GraphicsDevice.Clear(Color.White);

            GraphicsDevice.SetBlendState(GraphicsDevice.BlendStates.AlphaBlend);
            GraphicsDevice.SetDepthStencilState(GraphicsDevice.DepthStencilStates.None);

            DrawEdgesPass.Apply();
            GraphicsDevice.Draw(PrimitiveType.PointList, EdgesBuffer.ElementCount);

            DrawNodesPass.Apply();
            GraphicsDevice.Draw(PrimitiveType.PointList, NodesBuffer.ElementCount);

            Shader.Parameters["Nodes"].SetResource(NodesBuffer);
            Shader.Parameters["NewNodes"].SetResource(NewNodesBuffer);

            var dimx = (_users.Length + (BlockSize - 1)) / BlockSize;
            Shader.Parameters["numParticles"].SetValue(_users.Length);
            Shader.Parameters["dimx"].SetValue(dimx);

            const int iterations = 6 * 60;
            for (var i = 0; i < iterations; i++)
            {
                var dt = MathUtil.Lerp(1 / 60f, 6 / 60f, (float)i / iterations);

                //Shader.Parameters["dt"].SetValue(dt);

                UpdateNodesPass.Apply();
                GraphicsDevice.Dispatch(NodesBuffer.ElementCount, 1, 1);

                GraphicsDevice.Copy(NewNodesBuffer, NodesBuffer);
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            var dt = drawTimer.ElapsedMilliseconds / 1000f;
            drawTimer.Restart();

            var keyState = KeyboardManager.GetState();
            if (keyState.IsKeyDown(Keys.OemPlus))
                _scale = (float)Math.Exp(Math.Log(_scale) + dt);
            if (keyState.IsKeyDown(Keys.OemMinus))
                _scale = (float)Math.Exp(Math.Log(_scale) - dt);

            const float camVelocity = 1f;
            var translition = Vector2.Zero;
            if (keyState.IsKeyDown(Keys.Left))
                translition.X -= camVelocity / _scale * dt;
            if (keyState.IsKeyDown(Keys.Right))
                translition.X += camVelocity / _scale * dt;
            if (keyState.IsKeyDown(Keys.Down))
                translition.Y -= camVelocity / _scale * dt;
            if (keyState.IsKeyDown(Keys.Up))
                translition.Y += camVelocity / _scale * dt;

            var sin = (float)Math.Sin(-_rotation);
            var cos = (float)Math.Cos(-_rotation);
            _position.X += cos * translition.X - sin * translition.Y;
            _position.Y += sin * translition.X + cos * translition.Y;

            const float rotationVelocity = MathUtil.TwoPi / 4f;

            if (keyState.IsKeyDown(Keys.Q))
                _rotation += rotationVelocity * dt;
            if (keyState.IsKeyDown(Keys.E))
                _rotation -= rotationVelocity * dt;

            if (keyState.IsKeyDown(Keys.Escape))
                Exit();

            var matrix = Matrix.Translation(-_position.X, -_position.Y, 0) *
                         Matrix.RotationZ(_rotation) *
                         Matrix.Scaling(Scale2.X, Scale2.Y, 0);

            Shader.Parameters["projection"].SetValue(matrix);
            Shader.Parameters["scale"].SetValue(Scale2);
            //Shader.Parameters["camPos"].SetValue(_position);
            Shader.Parameters["EdgeBorderColor"].SetValue(new Color3(0.5f, 0.5f, 0.5f));
            Shader.Parameters["EdgeCenterColor"].SetValue(new Color3(0.8f, 0.8f, 0.8f));
            Shader.Parameters["Nodes"].SetResource(NodesBuffer);
            Shader.Parameters["NewNodes"].SetResource(NewNodesBuffer);

            foreach (var name in PropNames)
                Shader.Parameters[name].SetValue((float)typeof(Program).GetField(name).GetValue(this));

            GraphicsDevice.Clear(Color.White);

            GraphicsDevice.SetBlendState(GraphicsDevice.BlendStates.AlphaBlend);
            GraphicsDevice.SetDepthStencilState(GraphicsDevice.DepthStencilStates.None);

            DrawEdgesPass.Apply();
            GraphicsDevice.Draw(PrimitiveType.PointList, EdgesBuffer.ElementCount);

            DrawNodesPass.Apply();
            GraphicsDevice.Draw(PrimitiveType.PointList, NodesBuffer.ElementCount);

            for (var i = 0; i < 1; i++)
            {
                var dimx = (_users.Length + (BlockSize - 1)) / BlockSize;
                //Shader.Parameters["dt"].SetValue(dt);
                Shader.Parameters["numParticles"].SetValue(_users.Length);
                Shader.Parameters["dimx"].SetValue(dimx);

                UpdateNodesPass.Apply();
                GraphicsDevice.Dispatch(dimx, 1, 1);

                Shader.CurrentTechnique.Passes["UpdateNodesPosition"].Apply();
                GraphicsDevice.Dispatch(dimx, 1, 1);
            }

            Utilities.Swap(ref NodesBuffer, ref NewNodesBuffer);

            base.Draw(gameTime);
        }

        [STAThread]
        static void Main(string[] args)
        {
            using (var program = new Program())
                program.Run();
        }
    }

    class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Photo { get; set; }
        public int ArrayIndex { get; set; }

        public Vector2 Position { get; set; }

        public User[] AllFriends;
        public User[] Friends;
        public HashSet<User> AllFriendsSet;
        public HashSet<User> FriendsSet;

        public override int GetHashCode()
        {
            return Id;
        }
    }
}