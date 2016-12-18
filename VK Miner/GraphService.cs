using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json.Linq;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using SharpDX.Toolkit.Input;
using VK_Miner.VK;
using Buffer = SharpDX.Toolkit.Graphics.Buffer;
using Point = System.Windows.Point;

namespace VK_Miner
{
    public class GraphService : Game
    {
        private const int BlockSize = 128;
        private const int PhotoAtlasSize = 2048;
        private const int PhotoAtlasWidth = 200;

        private const float PhotoRadius = 2f;

        private const float SelectedPhotoWidth = 1.8f;
        private const float SelectedFriendsPhotoWidth = 1.4f;

        private const float EdgeNormalWidth = 0.06f;
        private const float EdgeSelectedWidth = 0.12f;

        private const long DoubleClickDelay = 200;

        private static readonly Color3 EdgeNormalBorderColor = new Color3(0.5f, 0.5f, 0.5f);
        private static readonly Color3 EdgeNormalCenterColor = new Color3(0.8f, 0.8f, 0.8f);

        private static readonly Color3 EdgeSelectedBorderColor = new Color3(0.8f, 0.0f, 0.0f);
        private static readonly Color3 EdgeSelectedCenterColor = new Color3(1.0f, 0.0f, 0.0f);

        public event Action<VK.Model.User> UserSelected;
        public event Action<VK.Model.User> UserNavigated;
        public bool DrawEdges { get; set; } = true;

        private readonly GraphicsDeviceManager _graphicsDeviceManager;
        private readonly KeyboardManager _keyboardManager;

        private float Aspect => (float)GraphicsDevice.BackBuffer.Width / GraphicsDevice.BackBuffer.Height;

        private Effect _shader;

        private EffectPass _drawNodesPass;
        private EffectPass _drawEdgesPass;
        private EffectPass _drawCirclesPass;
        private EffectPass _updateVelocityPass;
        private EffectPass _updatePositionPass;

        private Texture2D _defaultPhoto;
        private Texture2D[] _photoAtlases;

        private Buffer<NodeVertex> _nodesBuffer;
        private Buffer<NodeVertex> _newNodesBuffer;
        private Buffer<EdgeVertex> _edgesBuffer;
        private Buffer<EdgeVertex> _selectedEdgesBuffer;
        private Buffer<int> _edgeIndicesBuffer;

        private float _scale;
        private float _rotation;
        private Vector2 _position;

        private CancellationTokenSource _userLoadingCts;
        private ConcurrentQueue<Tuple<int, byte[]>> _loadedPhotosQueue;
        private ConcurrentQueue<Tuple<int, int[]>> _loadedFriendsQueue;

        private int _dimx;
        private User[] _users;
        private Dictionary<long, User> _usersById;

        private int _selectedUserId;
        private bool _mouseMoved;
        private Point _mouseDownPosition;
        private TimeSpan _lastMouseDownTime = DateTime.UtcNow.TimeOfDay;

        private bool _drawCircles;

        private float _repulsionForce = 2 / 60f;
        private float _repulsionDistance = 6f;
        private float _repulsionMax = 0.1f;
        private float _repulsionPower = 2;
        private float _attractionForce = 2 / 60f;
        private float _attractionDistance = 5;
        private float _attractionPower = 1;
        private float _dumping = 0.4f;
        private float _accelerationMinSquare = 0f;

        [Range(0, 20f / 60, "Сила отталкивания")]
        public float RepulsionForce
        {
            get { return _repulsionForce; }
            set
            {
                _repulsionForce = value;
                UpdateShaderParams();
            }
        }
        [Range(0, 10, "Расстояние отталкивания")]
        public float RepulsionDistance
        {
            get { return _repulsionDistance; }
            set
            {
                _repulsionDistance = value;
                UpdateShaderParams();
            }
        }
        [Range(0, 1, "Сглаживание отталкивания")]
        public float RepulsionMax
        {
            get { return _repulsionMax; }
            set
            {
                _repulsionMax = value;
                UpdateShaderParams();
            }
        }
        [Range(0, 5, "Степень отталкивания")]
        public float RepulsionPower
        {
            get { return _repulsionPower; }
            set
            {
                _repulsionPower = value;
                UpdateShaderParams();
            }
        }
        [Range(0, 10f / 60, "Сила притягивания")]
        public float AttractionForce
        {
            get { return _attractionForce; }
            set
            {
                _attractionForce = value;
                UpdateShaderParams();
            }
        }
        [Range(0, 10, "Расстояние притягивания")]
        public float AttractionDistance
        {
            get { return _attractionDistance; }
            set
            {
                _attractionDistance = value;
                UpdateShaderParams();
            }
        }
        [Range(0, 5, "Степень притягивания")]
        public float AttractionPower
        {
            get { return _attractionPower; }
            set
            {
                _attractionPower = value;
                UpdateShaderParams();
            }
        }
        [Range(0, 4, "Сопротивление воздуха")]
        public float Dumping
        {
            get { return _dumping; }
            set
            {
                _dumping = value;
                UpdateShaderParams();
            }
        }
        [Range(0, 1, "Минимальная сила")]
        public float AccelerationMinSquare
        {
            get { return _accelerationMinSquare; }
            set
            {
                _accelerationMinSquare = value;
                UpdateShaderParams();
            }
        }

        public GraphService(SharpDXElement surface)
        {
            _graphicsDeviceManager = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = (int)surface.ActualWidth,
                PreferredBackBufferHeight = (int)surface.ActualHeight,
                SynchronizeWithVerticalRetrace = true
            };
            _keyboardManager = new KeyboardManager(this);
            Content.RootDirectory = Path.Combine(Environment.CurrentDirectory, "Assets");

            surface.ManipulationDelta += SurfaceOnManipulationDelta;
            surface.MouseWheel += SurfaceOnMouseWheel;
            surface.MouseLeftButtonUp += SurfaceOnMouseLeftButtonUp;
            surface.MouseMove += SurfaceOnMouseMove;
            surface.MouseLeftButtonDown += SurfaceOnMouseLeftButtonDown;
        }

        public void UpdateShaderParams()
        {
            _shader.Parameters["RepulsionForce"].SetValue(RepulsionForce);
            _shader.Parameters["RepulsionDistance"].SetValue(RepulsionDistance);
            _shader.Parameters["RepulsionMax"].SetValue(RepulsionMax);
            _shader.Parameters["RepulsionPower"].SetValue(RepulsionPower);
            _shader.Parameters["AttractionForce"].SetValue(AttractionForce);
            _shader.Parameters["AttractionDistance"].SetValue(AttractionDistance);
            _shader.Parameters["AttractionPower"].SetValue(AttractionPower);
            _shader.Parameters["Dumping"].SetValue(Dumping);
            _shader.Parameters["AccelerationMinSquare"].SetValue(AccelerationMinSquare);
        }

        public void UpdateColors(bool draw)
        {
            if (draw)
            {
                _drawCircles = true;
                var nodes = _nodesBuffer.GetData();
                for (var i = 0; i < _users.Length; i++)
                    nodes[i].Color = _users[i].Color;
                _nodesBuffer.SetData(nodes);
                _newNodesBuffer.SetData(nodes);
            }
            else
            {
                _drawCircles = false;
            }
        }

        private void SurfaceOnMouseMove(object sender, MouseEventArgs e)
        {
            var delta = e.GetPosition(null) - _mouseDownPosition;

            if (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5)
                _mouseMoved = true;
        }
        private void SurfaceOnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_mouseMoved)
                return;

            var surface = (FrameworkElement)sender;
            var point = e.GetPosition(surface);

            var origin = _position + new Vector2(
                (float)((point.X / surface.ActualWidth * 2 - 1) / _scale * Aspect),
                (float)((1 - point.Y / surface.ActualHeight * 2) / _scale))
                .Rotate(-_rotation);

            var prevSelectedId = _selectedUserId;

            var minDistance = float.PositiveInfinity;
            _selectedUserId = -1;
            var nodes = _nodesBuffer.GetData();
            for (var i = 0; i < _users.Length; i++)
            {
                var distance = (nodes[i].Position - origin).Length();
                if (distance < PhotoRadius && distance < minDistance)
                {
                    minDistance = distance;
                    _selectedUserId = i;
                }
            }

            if (prevSelectedId == _selectedUserId)
                return;

            for (var i = 0; i < _users.Length; i++)
                nodes[i].Width = 1f;

            _selectedEdgesBuffer?.Dispose();
            _selectedEdgesBuffer = null;

            if (_selectedUserId != -1)
            {
                nodes[_selectedUserId].Width = SelectedPhotoWidth;
                foreach (var friend in _users[_selectedUserId].AllFriends)
                    nodes[friend].Width = SelectedFriendsPhotoWidth;

                var edges = _users[_selectedUserId].AllFriends.Select(i => new EdgeVertex(_selectedUserId, i)).ToArray();
                if (edges.Length != 0)
                    _selectedEdgesBuffer = Buffer.Structured.New(GraphicsDevice, edges);

                UserSelected?.Invoke(_users[_selectedUserId].Model);
            }

            _nodesBuffer.SetData(nodes);
            _newNodesBuffer.SetData(nodes);
        }
        private void SurfaceOnMouseLeftButtonDown(object sender, MouseEventArgs e)
        {
            _mouseDownPosition = e.GetPosition(null);
            _mouseMoved = false;

            var time = DateTime.UtcNow.TimeOfDay;
            if ((time - _lastMouseDownTime).TotalMilliseconds < DoubleClickDelay)
            {
                _mouseMoved = true;

                var surface = (FrameworkElement)sender;
                var point = e.GetPosition(surface);

                var origin = _position + new Vector2(
                    (float)((point.X / surface.ActualWidth * 2 - 1) / _scale * Aspect),
                    (float)((1 - point.Y / surface.ActualHeight * 2) / _scale))
                    .Rotate(-_rotation);

                var minDistance = float.PositiveInfinity;
                _selectedUserId = -1;
                var nodes = _nodesBuffer.GetData();
                for (var i = 0; i < _users.Length; i++)
                {
                    var distance = (nodes[i].Position - origin).Length();
                    if (distance < PhotoRadius && distance < minDistance)
                    {
                        minDistance = distance;
                        _selectedUserId = i;
                    }
                }

                if (_selectedUserId != -1)
                {
                    var user = _users[_selectedUserId].Model;
                    UserSelected?.Invoke(user);
                    UserNavigated?.Invoke(user);
                }
            }
            _lastMouseDownTime = time;
        }

        private void SurfaceOnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            const float wheelSpeed = 0.001f;

            var surface = (FrameworkElement)sender;
            var point = e.GetPosition(surface);
            var deltaScale = (float)Math.Exp(e.Delta * wheelSpeed);

            var origin = new Vector2(
                (float)((point.X / surface.ActualWidth * 2 - 1) / _scale * Aspect),
                (float)((1 - point.Y / surface.ActualHeight * 2) / _scale))
                .Rotate(-_rotation);

            _scale *= deltaScale;

            _position += origin - origin / deltaScale;
        }
        private void SurfaceOnManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            var surface = (FrameworkElement)sender;

            var angleDelta = (float)e.DeltaManipulation.Rotation / 180 * MathUtil.Pi;

            var delta = e.DeltaManipulation.Translation;
            delta.X *= (float)(2f / surface.ActualWidth * Aspect / _scale);
            delta.Y *= (float)(-2f / surface.ActualHeight / _scale);

            _position -= new Vector2((float)delta.X, (float)delta.Y).Rotate(-_rotation);

            var origin = new Vector2(
                (float)((e.ManipulationOrigin.X / surface.ActualWidth * 2 - 1) / _scale * Aspect),
                (float)((1 - e.ManipulationOrigin.Y / surface.ActualHeight * 2) / _scale))
                .Rotate(-_rotation);

            var deltaScale = 0.5f * (float)(e.DeltaManipulation.Scale.X + e.DeltaManipulation.Scale.Y);
            _scale *= deltaScale;
            _rotation -= angleDelta;

            _position += origin - origin.Rotate(angleDelta) / deltaScale;
        }

        public void InitializeNodes(Api api, User[] users)
        {
            _users = users;
            _usersById = users.ToDictionary(i => i.Model.Id);

            var nodes = new NodeVertex[_users.Length];
            for (var i = 0; i < _users.Length; i++)
            {
                var angle = 3.2 * Math.Sqrt(i + 1);
                var position = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * (float)angle * 0.25f;

                nodes[i] = new NodeVertex(position, i, 1, 0, 0);
            }

            _nodesBuffer?.Dispose();
            _newNodesBuffer?.Dispose();

            _nodesBuffer = Buffer.Structured.New(GraphicsDevice, nodes, true);
            _newNodesBuffer = Buffer.Structured.New(GraphicsDevice, nodes, true);

            _edgesBuffer?.Dispose();
            _edgesBuffer = null;
            _selectedEdgesBuffer?.Dispose();
            _selectedEdgesBuffer = null;
            _edgeIndicesBuffer?.Dispose();
            _edgeIndicesBuffer = null;

            _shader.Parameters["Edges"].SetResource(_edgesBuffer);
            _shader.Parameters["EdgeIndices"].SetResource(_edgeIndicesBuffer);

            if (_photoAtlases != null)
                foreach (var atlas in _photoAtlases)
                    atlas.Dispose();
            _photoAtlases = new Texture2D[(_users.Length + (PhotoAtlasSize - 1)) / PhotoAtlasSize];
            for (var i = 0; i < _photoAtlases.Length; i++)
            {
                var size = Math.Min(PhotoAtlasSize, _users.Length - i * PhotoAtlasSize);
                _photoAtlases[i] = Texture2D.New(GraphicsDevice, PhotoAtlasWidth, PhotoAtlasWidth, PixelFormat.R8G8B8A8.UNorm, arraySize: size);
                for (var j = 0; j < size; j++)
                    GraphicsDevice.Copy(_defaultPhoto, 0, _photoAtlases[i], j);
            }

            _scale = 1 / nodes.Last().Position.Length();
            _position = Vector2.Zero;
            _rotation = 0;

            _dimx = (_users.Length + (BlockSize - 1)) / BlockSize;
            _shader.Parameters["numParticles"].SetValue(_users.Length);
            _shader.Parameters["dimx"].SetValue(_dimx);

            _loadedPhotosQueue = new ConcurrentQueue<Tuple<int, byte[]>>();
            _loadedFriendsQueue = new ConcurrentQueue<Tuple<int, int[]>>();

            if (_userLoadingCts != null)
            {
                _userLoadingCts.Cancel();
                _userLoadingCts.Dispose();
            }

            _userLoadingCts = new CancellationTokenSource();
            Task.Factory.StartNew(() => StartPhotoLoadingTask(_userLoadingCts.Token), _userLoadingCts.Token);
            Task.Factory.StartNew(() => StartFriendsLoadingTask(api, _userLoadingCts.Token), _userLoadingCts.Token);
        }

        public void SortNodes()
        {
            var nodes = _nodesBuffer.GetData();
            for (var i = 0; i < _users.Length; i++)
            {
                var angle = 3.2 * Math.Sqrt(i + 1);
                var position = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * (float)angle * 0.25f;

                nodes[i].Position = position;
                nodes[i].Velocity = Vector2.Zero;
            }

            _nodesBuffer.SetData(nodes);
            _newNodesBuffer.SetData(nodes);
        }

        private void StartPhotoLoadingTask(CancellationToken token)
        {
            var queue = _loadedPhotosQueue;
            var options = new ParallelOptions()
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = 32
            };

            try
            {
                Parallel.ForEach(_users, options, user =>
                {
                    try
                    {
                        using (var client = new WebClient())
                        {
                            var data = client.DownloadData(user.Model.PhotoMax);
                            queue.Enqueue(Tuple.Create(user.ArrayIndex, data));
                        }
                    }
                    catch (WebException)
                    {
                    }
                    token.ThrowIfCancellationRequested();
                });
            }
            catch (OperationCanceledException) { }
        }
        private void StartFriendsLoadingTask(Api api, CancellationToken token)
        {
            const int usersPerRequest = 25;
            var queue = _loadedFriendsQueue;
            var users = _users;
            var request = new List<object>();
            for (var i = 0; i < users.Length; i += 25)
            {
                request.Clear();
                for (var j = 0; j < usersPerRequest && i + j < users.Length; j++)
                {
                    request.Add("u" + j);
                    request.Add(users[i + j].Model.Id);
                }

                var response = api.Run("execute.getFriends", request.ToArray());
                var items = (JArray)response["response"];

                for (var j = 0; j < items.Count; j++)
                {
                    queue.Enqueue(
                        Tuple.Create(
                            i + j,
                            items[j]
                                .Select(id => (long)id)
                                .Where(id => _usersById.ContainsKey(id))
                                .Select(id => _usersById[id].ArrayIndex)
                                .ToArray()));
                }

                token.ThrowIfCancellationRequested();
            }
        }

        private bool TryProcessPhoto()
        {
            Tuple<int, byte[]> item;
            if (_loadedPhotosQueue.TryDequeue(out item))
            {
                using (var mem = new MemoryStream(item.Item2))
                using (var buffer = Texture2D.Load(GraphicsDevice, mem))
                {
                    if (buffer.Description.Width == PhotoAtlasWidth / 2)
                    {
                        var smallWidth = buffer.Description.Width;
                        var smallHeight = buffer.Description.Height;
                        var small = buffer.GetData<Color>();
                        var big = new Color[PhotoAtlasWidth * PhotoAtlasWidth];
                        for (var i = 0; i < smallHeight; i++)
                        {
                            for (var j = 0; j < smallWidth; j++)
                            {
                                var color = small[i * smallWidth + j];
                                big[(i * 2) * PhotoAtlasWidth + (j * 2)] = color;
                                big[(i * 2) * PhotoAtlasWidth + (j * 2) + 1] = color;
                                big[(i * 2 + 1) * PhotoAtlasWidth + (j * 2)] = color;
                                big[(i * 2 + 1) * PhotoAtlasWidth + (j * 2) + 1] = color;
                            }
                        }
                        using (var buffer2 = Texture2D.New(GraphicsDevice, PhotoAtlasWidth, PhotoAtlasWidth, PixelFormat.R8G8B8A8.UNorm, big))
                            GraphicsDevice.Copy(buffer2, 0, _photoAtlases[item.Item1 / PhotoAtlasSize], item.Item1 % PhotoAtlasSize);
                    }
                    else
                        GraphicsDevice.Copy(buffer, 0, _photoAtlases[item.Item1 / PhotoAtlasSize], item.Item1 % PhotoAtlasSize);
                }

                return true;
            }
            else
            {
                return false;
            }
        }
        private void TryProcessFriends()
        {
            var updated = false;
            Tuple<int, int[]> item;
            while (_loadedFriendsQueue.TryDequeue(out item))
            {
                updated = true;

                var userId = item.Item1;
                var friendIds = item.Item2;
                var user = _users[userId];

                user.AllFriends.UnionWith(friendIds);
                foreach (var friendId in friendIds)
                {
                    _users[friendId].AllFriends.Add(userId);
                    if (!_users[friendId].Friends.Contains(userId))
                        user.Friends.Add(friendId);
                }
            }

            if (!updated)
                return;

            var nodes = _nodesBuffer.GetData();
            var edgesIndices = new int[_users.Sum(i => i.AllFriends.Count)];
            var edges = new EdgeVertex[_users.Sum(i => i.Friends.Count)];
            for (int i = 0, j = 0, k = 0; i < _users.Length; i++)
            {
                nodes[i].EdgesStart = j;
                nodes[i].EdgesEnd = j + _users[i].AllFriends.Count;

                foreach (var friendId in _users[i].AllFriends)
                    edgesIndices[j++] = friendId;

                foreach (var friendId in _users[i].Friends)
                    edges[k++] = new EdgeVertex(i, friendId);
            }

            _nodesBuffer.SetData(nodes);
            _newNodesBuffer.SetData(nodes);

            _edgeIndicesBuffer?.Dispose();
            _edgesBuffer?.Dispose();

            _edgeIndicesBuffer = edgesIndices.Length == 0 ? null : Buffer.Structured.New(GraphicsDevice, edgesIndices);
            _edgesBuffer = edges.Length == 0 ? null : Buffer.Structured.New(GraphicsDevice, edges.ToArray());

            _shader.Parameters["EdgeIndices"].SetResource(_edgeIndicesBuffer);
        }

        private void InitializeShader()
        {
            _shader = Content.Load<Effect>("Shader");
            _drawNodesPass = _shader.CurrentTechnique.Passes["DrawNodes"];
            _drawEdgesPass = _shader.CurrentTechnique.Passes["DrawEdges"];
            _drawCirclesPass = _shader.CurrentTechnique.Passes["DrawCircles"];
            _updateVelocityPass = _shader.CurrentTechnique.Passes["UpdateNodes"];
            _updatePositionPass = _shader.CurrentTechnique.Passes["UpdateNodesPosition"];

            _shader.Parameters["atlasSampler"].SetResource(GraphicsDevice.SamplerStates.LinearWrap);
            _shader.Parameters["pixelWidth"].SetValue(1f / GraphicsDevice.BackBuffer.Width);

            UpdateShaderParams();
        }

        private void UpdateCamera(float dt)
        {
            var keyState = _keyboardManager.GetState();

            if (keyState.IsKeyDown(Keys.OemPlus)) _scale = (float)Math.Exp(Math.Log(_scale) + dt);
            if (keyState.IsKeyDown(Keys.OemMinus)) _scale = (float)Math.Exp(Math.Log(_scale) - dt);

            const float camVelocity = 1f;
            var translition = Vector2.Zero;

            if (keyState.IsKeyDown(Keys.Left)) translition.X -= camVelocity / _scale * dt;
            if (keyState.IsKeyDown(Keys.Right)) translition.X += camVelocity / _scale * dt;
            if (keyState.IsKeyDown(Keys.Down)) translition.Y -= camVelocity / _scale * dt;
            if (keyState.IsKeyDown(Keys.Up)) translition.Y += camVelocity / _scale * dt;

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
                Application.Current.Shutdown();

            var scaling = new Vector2(_scale * GraphicsDevice.BackBuffer.Height / GraphicsDevice.BackBuffer.Width, _scale);
            var matrix = Matrix.Translation(-_position.X, -_position.Y, 0) *
                         Matrix.RotationZ(_rotation) *
                         Matrix.Scaling(scaling.X, scaling.Y, 0);

            _shader.Parameters["projection"].SetValue(matrix);
            _shader.Parameters["scale"].SetValue(scaling);
        }

        protected override void Initialize()
        {
            Window.IsMouseVisible = true;

            _defaultPhoto = Texture2D.Load(GraphicsDevice, "Assets\\camera_a.gif");
            InitializeShader();

            base.Initialize();
        }

        protected override void Draw(GameTime gameTime)
        {
            UpdateCamera((float)gameTime.ElapsedGameTime.TotalSeconds);

            GraphicsDevice.Clear(Color.Black);

            if (_users != null)
            {
                for (var i = 0; i < 10 && TryProcessPhoto(); i++) { }
                TryProcessFriends();

                _shader.Parameters["Nodes"].SetResource(_nodesBuffer);
                _shader.Parameters["NewNodes"].SetResource(_newNodesBuffer);

                GraphicsDevice.SetDepthStencilState(GraphicsDevice.DepthStencilStates.None);

                if (_drawCircles)
                {
                    GraphicsDevice.SetBlendState(GraphicsDevice.BlendStates.NonPremultiplied);
                    _drawCirclesPass.Apply();
                    GraphicsDevice.Draw(PrimitiveType.PointList, _users.Length);
                }

                GraphicsDevice.SetBlendState(GraphicsDevice.BlendStates.AlphaBlend);

                if (DrawEdges && _edgesBuffer != null)
                {
                    _shader.Parameters["EdgeBorderColor"].SetValue(EdgeNormalBorderColor);
                    _shader.Parameters["EdgeCenterColor"].SetValue(EdgeNormalCenterColor);
                    _shader.Parameters["edgeWidth"].SetValue(EdgeNormalWidth);
                    _shader.Parameters["Edges"].SetResource(_edgesBuffer);
                    _drawEdgesPass.Apply();
                    GraphicsDevice.Draw(PrimitiveType.PointList, _edgesBuffer.ElementCount);
                }

                if (_selectedEdgesBuffer != null)
                {
                    _shader.Parameters["EdgeBorderColor"].SetValue(EdgeSelectedBorderColor);
                    _shader.Parameters["EdgeCenterColor"].SetValue(EdgeSelectedCenterColor);
                    _shader.Parameters["edgeWidth"].SetValue(EdgeSelectedWidth);
                    _shader.Parameters["Edges"].SetResource(_selectedEdgesBuffer);
                    _drawEdgesPass.Apply();
                    GraphicsDevice.Draw(PrimitiveType.PointList, _selectedEdgesBuffer.ElementCount);
                }

                for (var i = 0; i < _photoAtlases.Length; i++)
                {
                    _shader.Parameters["vertexIdOffset"].SetValue(i * PhotoAtlasSize);
                    _shader.Parameters["atlas"].SetResource(_photoAtlases[i]);
                    _drawNodesPass.Apply();
                    GraphicsDevice.Draw(PrimitiveType.PointList, _photoAtlases[i].Description.ArraySize);
                }

                _updateVelocityPass.Apply();
                GraphicsDevice.Dispatch(_dimx, 1, 1);

                _updatePositionPass.Apply();
                GraphicsDevice.Dispatch(_dimx, 1, 1);

                Utilities.Swap(ref _nodesBuffer, ref _newNodesBuffer);
            }

            base.Draw(gameTime);
        }

        #region Vertices
        [StructLayout(LayoutKind.Sequential)]
        internal struct NodeVertex
        {
            [VertexElement("POSITION", 0, Format.R32G32_Float)]
            public Vector2 Position;
            [VertexElement("VELOCITY", 0, Format.R32G32_Float)]
            public Vector2 Velocity;
            [VertexElement("TEXCOORD", 0, Format.R32_Float)]
            public float AtlasIndex;
            [VertexElement("WIDTH", 0, Format.R32_Float)]
            public float Width;
            [VertexElement("EDGESSTART", 0, Format.R32_SInt)]
            public int EdgesStart;
            [VertexElement("EDGESEND", 0, Format.R32_SInt)]
            public int EdgesEnd;
            [VertexElement("COLOR", 0, Format.R32G32B32_Float)]
            public Color3 Color;
            public NodeVertex(Vector2 position, int atlasIndex, float width, int edgesStart, int edgesCount)
            {
                Position = position;
                Velocity = Vector2.Zero;
                AtlasIndex = atlasIndex;
                Width = width;
                EdgesStart = edgesStart;
                EdgesEnd = edgesStart + edgesCount;
                Color = Color3.Black;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct EdgeVertex
        {
            [VertexElement("POSITION", 0, Format.R32_SInt)]
            public int Source;
            [VertexElement("POSITION", 1, Format.R32_SInt)]
            public int Target;

            public EdgeVertex(int source, int target)
            {
                Source = source;
                Target = target;
            }
        }
    }
    #endregion
}
