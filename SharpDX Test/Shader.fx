struct Node
{
	float2 Position : POSITION;
	float2 Velocity : VELOCITY;
	float AtlasIndex : TEXCOORD;
	float Width : WIDTH;
	int EdgesStart : EDGESSTART;
	int EdgesEnd : EDGESEND;
	float3 Color : COLOR;
};
struct NodeGSIN
{
	float2 Position : POSITION;
	float AtlasIndex : TEXCOORD;
	float Width : WIDTH;
};
struct NodePixel
{
	float4 Position : SV_POSITION;
	float3 UV : TEXCOORD;
	float4 ShadowColor : COLOR;
};
struct Edge
{
	int Source : POSITION0;
	int Target : POSITION1;
};
struct EdgePixel
{
	float4 Position : SV_POSITION;
	float T : POSITION;
};
struct CircleGSIN 
{
	float2 Position : POSITION;
	float3 Color : COLOR;
};
struct CirclePixel 
{
	float4 Position : SV_POSITION;
	float2 UV : TEXCOORD;
	float3 Color : COLOR;
};

#define blockSize 128

static const float NodeBorder = 0.05;
static const float EdgeBorder = 0.5;
static const float ShadowLevel = 0.2;

static const float MinDistance = 0.00000001;
static const float CenteringForce = 0.0001;

static const float4 NormalShadowColor = float4(0, 0, 0, 1);
static const float4 SelectedShadowColor = float4(1.5, 0, 0, 1000);

static const float CircleRadius = 3;

sampler atlasSampler;
Texture2DArray atlas;

StructuredBuffer<Node> Nodes;
RWStructuredBuffer<Node> NewNodes;

StructuredBuffer<Edge> Edges;
StructuredBuffer<int> EdgeIndices;

groupshared float2 sharedPos[blockSize];

matrix projection;
float2 scale;
float pixelWidth;
float edgeWidth;

float3 EdgeBorderColor;
float3 EdgeCenterColor;

uint numParticles, dimx, vertexIdOffset;
uint selectedNode;

float RepulsionForce;
float RepulsionDistance;
float RepulsionMax;
float RepulsionPower;
float AttractionForce;
float AttractionDistance;
float AttractionPower;
float Dumping;
float AccelerationMinSquare;

float2 Project(float2 world)
{
	float4 result = mul(float4(world, 0, 1), projection);
	return result.xy / result.w;
}

NodeGSIN NodeVS(uint index : SV_VERTEXID)
{
	Node node = Nodes[index + vertexIdOffset];
	NodeGSIN output;
	output.Position = node.Position;
	output.AtlasIndex = node.AtlasIndex;
	output.Width = node.Width;
	return output;
}

[maxvertexcount(4)]
void NodeGS(point NodeGSIN input[1], inout TriangleStream<NodePixel> stream)
{
	NodeGSIN center = input[0];
	NodePixel pixel;
	pixel.Position.zw = float2(0, 1);
	pixel.UV.z = center.AtlasIndex;
	pixel.ShadowColor = center.Width == 1 ? NormalShadowColor : SelectedShadowColor;

	float2 pos = Project(center.Position.xy);

	float2 width = scale * center.Width;

	pixel.Position.xy = pos + float2(-0.5, 0.5) * width;
	pixel.UV.xy = float2(0, 0);
	stream.Append(pixel);

	pixel.Position.xy = pos + float2(0.5, 0.5) * width;
	pixel.UV.xy = float2(1, 0);
	stream.Append(pixel);

	pixel.Position.xy = pos + float2(-0.5, -0.5) * width;
	pixel.UV.xy = float2(0, 1);
	stream.Append(pixel);

	pixel.Position.xy = pos + float2(0.5, -0.5) * width;
	pixel.UV.xy = float2(1, 1);
	stream.Append(pixel);
}

float4 NodePS(NodePixel input) : SV_TARGET
{
	float2 t = (float2(0.5, 0.5) - abs(input.UV - float2(0.5, 0.5))) * (1 / NodeBorder);
	float shadow;
	if (t.x < 1 && t.y < 1)
	{
		shadow = 1 - distance(t, float2(1, 1));
		if (shadow < 0) discard;
	}
	else if (t.x < 1)
		shadow = t.x;
	else if (t.y < 1)
		shadow = t.y;
	else
		shadow = 2;

	if (shadow > 1)
		return atlas.Sample(atlasSampler, input.UV);
	else
		return input.ShadowColor * float4(1, 1, 1, shadow * ShadowLevel);
}

Edge EdgeVS(uint index : SV_VERTEXID)
{
	return Edges[index];
}

[maxvertexcount(4)]
void EdgeGS(point Edge input[1], inout TriangleStream<EdgePixel> stream)
{
	float2 p0 = Nodes[input[0].Source].Position;
	float2 p1 = Nodes[input[0].Target].Position;

	float r = 0.5 * pixelWidth / scale.x;

	float2 oX = normalize(p1 - p0);
	float2 oY = float2(-oX.y, oX.x);
	oX *= min(0.5 - NodeBorder, r);
	oY *= edgeWidth * 0.5 + r;

	EdgePixel pixel;
	pixel.Position.zw = float2(0, 1);

	pixel.Position.xy = Project(p0 + oX + oY);
	pixel.T = 0;
	stream.Append(pixel);

	pixel.Position.xy = Project(p1 - oX + oY);
	stream.Append(pixel);

	pixel.Position.xy = Project(p0 + oX - oY);
	pixel.T = 1;
	stream.Append(pixel);

	pixel.Position.xy = Project(p1 - oX - oY);
	stream.Append(pixel);
}

float4 EdgePS(EdgePixel pixel) : SV_TARGET
{
	float x = min(EdgeBorder, 0.5 - abs(pixel.T - 0.5)) / EdgeBorder;
	return float4(lerp(EdgeBorderColor, EdgeCenterColor, x), 1);
}

CircleGSIN CircleVS(uint index : SV_VERTEXID) 
{
	Node node = Nodes[index];
	CircleGSIN output;
	output.Position = node.Position;
	output.Color = node.Color;
	return output;
}

[maxvertexcount(4)]
void CircleGS(point CircleGSIN input[1], inout TriangleStream<CirclePixel> stream) 
{
	CircleGSIN center = input[0];

	CirclePixel pixel;
	pixel.Position.zw = float2(0, 1);
	pixel.Color = center.Color;

	if (pixel.Color.r == 0 && pixel.Color.g == 0 && pixel.Color.b == 0)
		return;

	float2 pos = Project(center.Position.xy);

	float2 width = scale * CircleRadius;

	pixel.Position.xy = pos + float2(-0.5, 0.5) * width;
	pixel.UV.xy = float2(-1, -1);
	stream.Append(pixel);

	pixel.Position.xy = pos + float2(0.5, 0.5) * width;
	pixel.UV.xy = float2(+1, -1);
	stream.Append(pixel);

	pixel.Position.xy = pos + float2(-0.5, -0.5) * width;
	pixel.UV.xy = float2(-1, +1);
	stream.Append(pixel);

	pixel.Position.xy = pos + float2(0.5, -0.5) * width;
	pixel.UV.xy = float2(+1, +1);
	stream.Append(pixel);
}

float4 CirclePS(CirclePixel pixel) : SV_TARGET
{
	float t = length(pixel.UV);
	if (t > 1)
		discard;

	return float4(pixel.Color, (1 - t) * (1 / 0.3));
}

[numthreads(blockSize, 1, 1)]
void UpdateCS(uint3 Gid : SV_GroupID, uint3 DTid : SV_DispatchThreadID, uint3 GTid : SV_GroupThreadID, uint GI : SV_GroupIndex)
{
	Node node = Nodes[DTid.x];

	float2 pos = node.Position;
	float2 vel = node.Velocity;
	double2 accel = 0;

	//[loop]
	for (uint tile = 0; tile < dimx; tile++)
	{
		int offset = tile * blockSize;

		sharedPos[GI] = Nodes[offset + GI].Position;

		GroupMemoryBarrierWithGroupSync();

		uint counterTo = min(blockSize, numParticles - offset);
		//[unroll]
		for (uint counter = 0; counter < counterTo; counter++)
		{
			if (DTid.x == offset + counter) continue;

			float2 delta = sharedPos[counter] - pos;
			float distance = length(delta);
			double2 direction = delta / max(distance, MinDistance);
			distance /= RepulsionDistance;

			double2 force = direction * (RepulsionForce / (pow(distance, RepulsionPower) + RepulsionMax));
			accel -= force;
		}

		GroupMemoryBarrierWithGroupSync();
	}

	for (uint i = node.EdgesStart; i < node.EdgesEnd; i++)
	{
		uint j = EdgeIndices[i];
		double2 delta = pos - Nodes[j].Position;
		double distance = length(delta);
		double invDistance = 1.0 / (float)max(distance, MinDistance);
		double2 force = AttractionForce * pow(distance - AttractionDistance, AttractionPower) * invDistance * delta;
		accel -= force;
	}

	accel -= pos * CenteringForce;

	if (dot(accel, accel) > AccelerationMinSquare)
		vel += (float2)(accel);

	vel *= exp(-Dumping);

	if (DTid.x < numParticles) {
		NewNodes[DTid.x].Position = pos;
		NewNodes[DTid.x].Velocity = vel;
	}
}

[numthreads(blockSize, 1, 1)]
void UpdatePositionCS(uint3 DTid : SV_DispatchThreadID)
{
	NewNodes[DTid.x].Position = Nodes[DTid.x].Position + NewNodes[DTid.x].Velocity;
}

technique11
{
	pass DrawNodes
	{
		Profile = 11;
		VertexShader = NodeVS;
		GeometryShader = NodeGS;
		PixelShader = NodePS;
	}
	pass DrawEdges
	{
		Profile = 11;
		VertexShader = EdgeVS;
		GeometryShader = EdgeGS;
		PixelShader = EdgePS;
	}
	pass DrawCircles 
	{
		Profile = 11;
		VertexShader = CircleVS;
		GeometryShader = CircleGS;
		PixelShader = CirclePS;
	}
	pass UpdateNodes
	{
		Profile = 11;
		ComputeShader = UpdateCS;
	}
	pass UpdateNodesPosition
	{
		Profile = 11;
		ComputeShader = UpdatePositionCS;
	}
}