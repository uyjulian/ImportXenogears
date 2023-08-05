// C# xenogears importer:
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;

public class FileEntry {
	public string name;
	public int pos;
	public int size;
}

public class Vertex : IComparable<Vertex> {
	public int u, v;
	public uint r, g, b, a;
	public int x,y,z;
	public int nx,ny,nz;
	public int CompareTo(Vertex rhs) {
		if (u != rhs.u) {
			return u.CompareTo(rhs.u);
		}
		if (v != rhs.v) {
			return v.CompareTo(rhs.v);
		}
		if (r != rhs.r) {
			return r.CompareTo(rhs.r);
		}
		if (g != rhs.g) {
			return g.CompareTo(rhs.g);
		}
		if (b != rhs.b) {
			return b.CompareTo(rhs.b);
		}
		if (a != rhs.a) {
			return a.CompareTo(rhs.a);
		}
		if (x != rhs.x) {
			return x.CompareTo(rhs.x);
		}
		if (y != rhs.y) {
			return y.CompareTo(rhs.y);
		}
		if (z != rhs.z) {
			return z.CompareTo(rhs.z);
		}
		if (nx != rhs.nx) {
			return nx.CompareTo(rhs.nx);
		}
		if (ny != rhs.ny) {
			return ny.CompareTo(rhs.ny);
		}
		return nz.CompareTo(rhs.nz);
	}
}

public class Triangle : IComparable<Triangle> {
	public int shader;
	public Vertex[] vertex;
	
	public int CompareTo(Triangle rhs) {
		if (shader != rhs.shader) {
			return shader.CompareTo(rhs.shader);
		}
		for(int j=0; j<vertex.Length; j++) {
			int compare = vertex[j].CompareTo(rhs.vertex[j]);
			if (compare != 0) {
				return compare;
			}
		}
		return 0;
	}
}

public class XGShader : IEquatable<XGShader> {
	public uint status;
	public uint clut;
	public bool abe;
	public bool tme;
	
	public XGShader(uint status, uint clut, bool abe, bool tme) {
		this.status = status;
		this.clut = clut;
		this.abe = abe;
		this.tme = tme;
	}
	
	public bool Equals(XGShader rhs) {
		if (rhs == null) {
			return false;
		}
		if (status != rhs.status) {
			return false;
		}
		if (clut != rhs.clut) {
			return false;
		}
		if (abe != rhs.abe) {
			return false;
		}
		if (tme != rhs.tme) {
			return false;
		}
		return true;
	}
}

public class XGMesh {
	public Mesh[] meshes;
	public int[] materials;
}

public class XGTexture {
	public int width;
	public int height;
	public string name;
	public uint[] pixels;
}

public class XGModel {
	public List<XGMesh> meshes;
	public List<XGShader> shaders;
}

public class ImportXenogears : EditorWindow {
	string filename = "";
	string disc1 = "Xenogears1.img";
	string disc2 = "Xenogears2.img";
	bool exportDiscs = true;
	bool exportField = true;
	string exportFieldIndices = "";
	bool attachXGFieldNode = true;
	bool exportStage = true;
	string exportStageIndices = "";
	bool exportTerrain = true;
	string exportTerrainIndices = "";
	bool exportHeads = true;
	string exportHeadIndices = "";
	bool exportSlides = true;
	string exportSlideIndices = "";
	bool exportSceneModel = true;
	string exportSceneModelIndices = "";
	bool doExport = false;
	bool debugFeatures = false;
	
	private const int discsVersion = 1;
	private const int fieldVersion = 1;
	private const int stageVersion = 1;
	private const int terrainVersion = 1;
	private const int headsVersion = 1;
	private const int slidesVersion = 1;
	private const int sceneVersion = 1;
	
    // Add menu named "My Window" to the Window menu
    [MenuItem ("Window/Import Xenogears")]
    static void Import () {
        // Get existing open window or if none, make a new one:
        ImportXenogears window = (ImportXenogears)EditorWindow.GetWindow (typeof (ImportXenogears), false, "Import");
    }
    
	static byte[] decompressLzs(byte[] ibuf, uint ofs, uint size) {
		byte[] obuf = new byte[size];
	    uint iofs = ofs;
		uint oofs = 0;
	    uint cmd = 0;
	    uint bit = 0;
	    while (iofs < ibuf.Length && oofs < obuf.Length) {
			if (bit == 0) {
			    cmd = ibuf[iofs++];
			    bit = 8;
			}
			if ((cmd & 1) != 0) {
			    byte a = ibuf[iofs++];
			    byte b = ibuf[iofs++];
			    uint o = (uint)a | ((uint)(b & 0x0F) << 8);
			    uint l = (((uint)b & 0xF0) >> 4) + 3;
			    int rofs = (int)oofs - (int)o;
				// UnityEngine.Debug.Log("oofs:"+oofs+" iofs:"+iofs+" rofs:"+rofs+" l:"+l+" o:"+o+" size:"+size+" a:"+a+" b:"+b);
			    for (int j=0; j<l; j++) {
					if (rofs < 0) {
						obuf[oofs++] = 0;
					} else {
					    obuf[oofs++] = obuf[rofs];
					}
					rofs++;
				}
			} else if(oofs < obuf.Length) {
				// UnityEngine.Debug.Log("oofs:"+oofs+" iofs:"+iofs);
			    obuf[oofs++] = ibuf[iofs++];
			}
			cmd >>= 1;
			bit -= 1;
		}
	    return obuf;
	}
	
	static byte[] loadLzs(string path) {
		byte[] buf = File.ReadAllBytes(path);	
		uint length = getUInt32LE(buf, 0);
		return decompressLzs(buf, 4, length);
	}
	
	static ushort getUInt16LE(byte[] buf, uint ofs) {
		return (ushort)(((ushort)buf[ofs+1] << 8) | (ushort)buf[ofs+0]);
	}
	
	static uint getUInt32LE(byte[] buf, uint ofs) {
		return ((uint)buf[ofs+3] << 24) | ((uint)buf[ofs+2] << 16) | ((uint)buf[ofs+1] << 8) | (uint)buf[ofs+0];
	}
	
	static byte[] getData(byte[] archiveData, uint index) {
		uint offset = getUInt32LE( archiveData, 0x0130 + index * 4);
	    uint dataEnd = (index < 8) ? getUInt32LE( archiveData, 0x0134 + index * 4) : (uint)archiveData.Length;
	    uint size = getUInt32LE(archiveData, 0x010c + index * 4);
		// UnityEngine.Debug.Log ("getData offset:"+offset+" size:"+size+" dataEnd:"+dataEnd);
		return decompressLzs(archiveData, offset + 4, size);
	}
	
	static int addShader(List<XGShader> shaderList, XGShader xgShader) {
		for(int i=0; i<shaderList.Count; i++) {
			if (shaderList[i].Equals(xgShader)) {
				return i;
			}
		}
		
		int idx = shaderList.Count;
		shaderList.Add(xgShader);
		return idx;
	}

	static string ToUnityPath(string path) {
		return path.Replace ('\\', '/');
	}

	static void splitIndicesString(string str, List<uint> lst) {
		string[] split = str.Split(new char[] { ',', ';', ':' });

		foreach (string strsp in split) {
			if (strsp.Length > 0) {
				lst.Add((uint)(UInt32.Parse(strsp)));
			}
		}
	}

	static void arr_deg_to_rad(float[] arr)
	{
		for (var i = 0; i < arr.Length; i += 1)
		{
			arr[i] = (arr[i] / 360.0f) * (2.0f * (float)Math.PI);
		}
	}

	static void arr_rad_to_quat(float[] arr)
	{
		var cy = (float)Math.Cos(arr[2] * 0.5f);
		var sy = (float)Math.Sin(arr[2] * 0.5f);
		var cp = (float)Math.Cos(arr[1] * 0.5f);
		var sp = (float)Math.Sin(arr[1] * 0.5f);
		var cr = (float)Math.Cos(arr[0] * 0.5f);
		var sr = (float)Math.Sin(arr[0] * 0.5f);
		arr[0] = cr * cp * cy + sr * sp * sy;
		arr[1] = sr * cp * cy - cr * sp * sy;
		arr[2] = cr * sp * cy + sr * cp * sy;
		arr[3] = cr * cp * sy - sr * sp * cy;
	}

	static void arr_normalize_deg(float[] arr)
	{
		for (var i = 0; i < arr.Length; i += 1)
		{
			arr[i] %= 360.0f;
			if (arr[i] < 0)
			{
				arr[i] += 360.0f;
			}
		}
	}

	static void arr_deg_to_quat(float[] arr)
	{
		arr_normalize_deg(arr);
		arr_deg_to_rad(arr);
		arr_rad_to_quat(arr);
	}

	static XGModel importFieldModel(byte[] data) {
		XGModel xgModel = new XGModel();
		xgModel.meshes = new List<XGMesh>();
		xgModel.shaders = new List<XGShader>();
		
		uint partCount = getUInt32LE(data, 0);
		for(uint partIndex = 0; partIndex < partCount; partIndex++) {
			uint partOffset = getUInt32LE(data, 4 + partIndex * 4);
			
			List<Triangle> triangleList = new List<Triangle>();

			uint blockCount = getUInt32LE(data, partOffset);
			for(uint blockIndex=0; blockIndex < blockCount; blockIndex++) {
				uint blockOffset = partOffset + 16 + blockIndex * 0x38;
				
				uint vertexCount = getUInt16LE(data, blockOffset + 2);
				uint meshCount = getUInt16LE(data, blockOffset + 4);
				uint meshBlockCount = getUInt16LE(data, blockOffset + 6);
				uint vertexOffset = getUInt32LE(data, blockOffset + 8);
				uint normalOffset = getUInt32LE(data, blockOffset + 12);
				uint meshBlockOffset = getUInt32LE(data, blockOffset + 16);
				uint displayListOffset = getUInt32LE(data, blockOffset + 20);

				uint status = 0;
				uint clut = 0;
				for(uint meshBlockIndex = 0; meshBlockIndex < meshBlockCount; meshBlockIndex++) {
					// init the mesh block
					uint quad_block = data[partOffset + meshBlockOffset];
					uint polyCount = getUInt16LE(data, partOffset + meshBlockOffset + 2);
					meshBlockOffset += 4;

					while (polyCount > 0) {
	
						// decode command
						uint cmd = getUInt32LE(data, partOffset + displayListOffset);
			    
						bool hp = ((cmd >> 24) & 16) != 0;	    // geraud shading
						bool quad = ((cmd >> 24) & 8) != 0;	    // quad or tri
						bool tme = ((cmd >> 24) & 4) != 0;	    // texture mapping
						bool abe = ((cmd >> 24) & 2) != 0;	    // semi transparency
						bool fullbright = ((cmd >> 24) & 1) != 0;	    // bypass lighting
						uint op = (cmd >> 24) & 255;		    // operator
						uint pop = (uint)(op & ~(16|2|1));		    // operator, with shading and lighting mask
						
						displayListOffset += 4;
			    		if (op == 0xC4) { // texture page
							status = cmd & 0xFFFF;
						} else if (op == 0xC8) { // clut
							clut = cmd & 0xFFFF;
						} else if (pop == 0x24) { // triangle with texture	
							int ua = data[partOffset + displayListOffset + 0];
							int va = data[partOffset + displayListOffset + 1];
							int ub = data[partOffset + displayListOffset + 2];
							int vb = data[partOffset + displayListOffset + 3];
							int uc = (int)(cmd & 255);
							int vc = (int)((cmd >> 8) & 255);
							displayListOffset += 4;

							int shader = addShader(xgModel.shaders, new XGShader(status, clut, abe, true));
							Vertex[] vertex = new Vertex[3];
							for(int j=0; j<3; j++) {
								vertex[j] = new Vertex();
							}
							vertex[0].u = ua;
							vertex[0].v = va;
							vertex[1].u = ub;
							vertex[1].v = vb;
							vertex[2].u = uc;
							vertex[2].v = vc;
							Vector3[] vec = new Vector3[3];
							for(uint j=0; j<3; j++) {
							    uint vtx = getUInt16LE(data, partOffset + meshBlockOffset + j * 2);
								vertex[j].x = (short)getUInt16LE(data, partOffset + vertexOffset + vtx * 8 + 0);
								vertex[j].y = (short)getUInt16LE(data, partOffset + vertexOffset + vtx * 8 + 2);
								vertex[j].z = (short)getUInt16LE(data, partOffset + vertexOffset + vtx * 8 + 4);
							    if (hp) {
									vertex[j].nx = (short)getUInt16LE(data, partOffset + normalOffset + vtx * 8 + 0);
									vertex[j].ny = (short)getUInt16LE(data, partOffset + normalOffset + vtx * 8 + 2);
									vertex[j].nz = (short)getUInt16LE(data, partOffset + normalOffset + vtx * 8 + 4);
								} else {
									vec[j].x = vertex[j].x;
									vec[j].y = vertex[j].y;
									vec[j].z = vertex[j].z;
								}
								vertex[j].r = 255;
								vertex[j].g = 255;
								vertex[j].b = 255;
								vertex[j].a = 255;
							}
							if (!hp) {
							    Vector3 d1 = vec[2] - vec[0];
							    Vector3 d2 = vec[1] - vec[0];
							    Vector3 n = (Vector3.Cross(d1, d2)).normalized;
								n.x *= 4096.0f;
								n.y *= 4096.0f;
								n.z *= 4096.0f;
								n.x += 0.5f;
								n.y += 0.5f;
								n.z += 0.5f;
								for(uint j=0; j<3; j++) {
									vertex[j].nx = (int)(n.x);
									vertex[j].ny = (int)(n.y);
									vertex[j].nz = (int)(n.z);
								}
							}

							Triangle tri = new Triangle();
							tri.shader = shader;
							tri.vertex = new Vertex[3];
							tri.vertex[0] = vertex[1];
							tri.vertex[1] = vertex[0];
							tri.vertex[2] = vertex[2];
							triangleList.Add(tri);
							
							meshBlockOffset += 8;
							polyCount --;
						} else if ( pop == 0x2C ) { // quad with texture					
							int ua = data[partOffset + displayListOffset + 0];
							int va = data[partOffset + displayListOffset + 1];
							int ub = data[partOffset + displayListOffset + 2];
							int vb = data[partOffset + displayListOffset + 3];
							int uc = data[partOffset + displayListOffset + 4];
							int vc = data[partOffset + displayListOffset + 5];
							int ud = data[partOffset + displayListOffset + 6];
							int vd = data[partOffset + displayListOffset + 7];
							displayListOffset += 8;
				
							int shader = addShader(xgModel.shaders, new XGShader(status, clut, abe, true));

							Vertex[] vertex = new Vertex[4];
							for(int j=0; j<4; j++) {
								vertex[j] = new Vertex();
							}
							vertex[0].u = ua;
							vertex[0].v = va;
							vertex[1].u = ub;
							vertex[1].v = vb;
							vertex[2].u = uc;
							vertex[2].v = vc;
							vertex[3].u = ud;
							vertex[3].v = vd;
							Vector3[] vec = new Vector3[4];
							for(uint j=0; j<4; j++) {
								uint vtx = getUInt16LE(data, partOffset + meshBlockOffset + j * 2);
								vertex[j].x = (short)getUInt16LE(data, partOffset + vertexOffset + vtx * 8 + 0);
								vertex[j].y = (short)getUInt16LE(data, partOffset + vertexOffset + vtx * 8 + 2);
								vertex[j].z = (short)getUInt16LE(data, partOffset + vertexOffset + vtx * 8 + 4);
							    if (hp) {
									vertex[j].nx = (short)getUInt16LE(data, partOffset + normalOffset + vtx * 8 + 0);
									vertex[j].ny = (short)getUInt16LE(data, partOffset + normalOffset + vtx * 8 + 2);
									vertex[j].nz = (short)getUInt16LE(data, partOffset + normalOffset + vtx * 8 + 4);
								} else {
									vec[j].x = vertex[j].x;
									vec[j].y = vertex[j].y;
									vec[j].z = vertex[j].z;
								}
								vertex[j].r = 255;
								vertex[j].g = 255;
								vertex[j].b = 255;
								vertex[j].a = 255;
							}
							if (!hp) {
							    Vector3 d1 = vec[2] - vec[0];
							    Vector3 d2 = vec[1] - vec[0];
							    Vector3 n = (Vector3.Cross(d1, d2)).normalized;
								n.x *= 4096.0f;
								n.y *= 4096.0f;
								n.z *= 4096.0f;
								n.x += 0.5f;
								n.y += 0.5f;
								n.z += 0.5f;
								
								for(uint j=0; j<4; j++) {
									vertex[j].nx = (int)(n.x);
									vertex[j].ny = (int)(n.y);
									vertex[j].nz = (int)(n.z);
								}
							}
							
							Triangle tri1 = new Triangle();
							tri1.shader = shader;
							tri1.vertex = new Vertex[3];
							tri1.vertex[0] = vertex[1];
							tri1.vertex[1] = vertex[0];
							tri1.vertex[2] = vertex[2];
							triangleList.Add(tri1);

							Triangle tri2 = new Triangle();
							tri2.shader = shader;
							tri2.vertex = new Vertex[3];
							tri2.vertex[0] = vertex[1];
							tri2.vertex[1] = vertex[2];
							tri2.vertex[2] = vertex[3];
							triangleList.Add(tri2);
							
							meshBlockOffset += 8;
							polyCount --;
						} else if (pop == 0x20) { // monochrome triangle
							uint r = (cmd >> 16) & 255;
							uint g = (cmd >> 8) & 255;
							uint b = (cmd) & 255;
							uint a = 255;
							if (abe) {
							    switch ((status >> 5) & 3) {
								case 0: a = 128; break;
								case 1: a = 0; break;
								case 2: a = 0; break;
								case 3: a = 64; break;
								}
							}
				
							int shader = addShader(xgModel.shaders, new XGShader(status, clut, abe, false));
							Vertex[] vertex = new Vertex[3];
							Vector3[] vec = new Vector3[3];
							for(uint j=0; j<3; j++) {
								vertex[j] = new Vertex();
								vertex[j].u = 0;
								vertex[j].v = 0;
								uint vtx = getUInt16LE(data, partOffset + meshBlockOffset + j * 2);
								vertex[j].x = (short)getUInt16LE(data, partOffset + vertexOffset + vtx * 8 + 0);
								vertex[j].y = (short)getUInt16LE(data, partOffset + vertexOffset + vtx * 8 + 2);
								vertex[j].z = (short)getUInt16LE(data, partOffset + vertexOffset + vtx * 8 + 4);
							    if (hp) {
									vertex[j].nx = (short)getUInt16LE(data, partOffset + normalOffset + vtx * 8 + 0);
									vertex[j].ny = (short)getUInt16LE(data, partOffset + normalOffset + vtx * 8 + 2);
									vertex[j].nz = (short)getUInt16LE(data, partOffset + normalOffset + vtx * 8 + 4);
								} else {
									vec[j].x = vertex[j].x;
									vec[j].y = vertex[j].y;
									vec[j].z = vertex[j].z;
								}
								vertex[j].r = r;
								vertex[j].g = g;
								vertex[j].b = b;
								vertex[j].a = a;
							}
							if (!hp) {
							    Vector3 d1 = vec[2] - vec[0];
							    Vector3 d2 = vec[1] - vec[0];
							    Vector3 n = (Vector3.Cross(d1, d2)).normalized;
								n.x *= 4096.0f;
								n.y *= 4096.0f;
								n.z *= 4096.0f;
								n.x += 0.5f;
								n.y += 0.5f;
								n.z += 0.5f;
								for(uint j=0; j<3; j++) {
									vertex[j].nx = (int)(n.x);
									vertex[j].ny = (int)(n.y);
									vertex[j].nz = (int)(n.z);
								}
							}
												
							Triangle tri = new Triangle();
							tri.shader = shader;
							tri.vertex = new Vertex[3];
							tri.vertex[0] = vertex[1];
							tri.vertex[1] = vertex[0];
							tri.vertex[2] = vertex[2];
							triangleList.Add(tri);
							
							meshBlockOffset += 8;
							polyCount --;
						} else if (pop == 0x28) { // monochrome quad
							uint r = (cmd >> 16) & 255;
							uint g = (cmd >> 8) & 255;
							uint b = (cmd) & 255;
							uint a = 255;
							if (abe) {
							    switch ((status >> 5) & 3) {
								case 0: a = 128; break;
								case 1: a = 0; break;
								case 2: a = 0; break;
								case 3: a = 64; break;
								}
							}
				
							int shader = addShader(xgModel.shaders, new XGShader(status, clut, abe, false));
							Vertex[] vertex = new Vertex[4];
							Vector3[] vec = new Vector3[4];
							for(uint j=0; j<4; j++) {
								vertex[j] = new Vertex();
								vertex[j].u = 0;
								vertex[j].v = 0;
								uint vtx = getUInt16LE(data, partOffset + meshBlockOffset + j * 2);
								vertex[j].x = (short)getUInt16LE(data, partOffset + vertexOffset + vtx * 8 + 0);
								vertex[j].y = (short)getUInt16LE(data, partOffset + vertexOffset + vtx * 8 + 2);
								vertex[j].z = (short)getUInt16LE(data, partOffset + vertexOffset + vtx * 8 + 4);
							    if (hp) {
									vertex[j].nx = (short)getUInt16LE(data, partOffset + normalOffset + vtx * 8 + 0);
									vertex[j].ny = (short)getUInt16LE(data, partOffset + normalOffset + vtx * 8 + 2);
									vertex[j].nz = (short)getUInt16LE(data, partOffset + normalOffset + vtx * 8 + 4);
								} else {
									vec[j].x = vertex[j].x;
									vec[j].y = vertex[j].y;
									vec[j].z = vertex[j].z;
								}
								vertex[j].r = r;
								vertex[j].g = g;
								vertex[j].b = b;
								vertex[j].a = a;
							}
							if (!hp) {
							    Vector3 d1 = vec[2] - vec[0];
							    Vector3 d2 = vec[1] - vec[0];
							    Vector3 n = (Vector3.Cross(d1, d2)).normalized;
								n.x *= 4096.0f;
								n.y *= 4096.0f;
								n.z *= 4096.0f;
								n.x += 0.5f;
								n.y += 0.5f;
								n.z += 0.5f;
								for(uint j=0; j<4; j++) {
									vertex[j].nx = (int)(n.x);
									vertex[j].ny = (int)(n.y);
									vertex[j].nz = (int)(n.z);
								}
							}
							
							Triangle tri1 = new Triangle();
							tri1.shader = shader;
							tri1.vertex = new Vertex[3];
							tri1.vertex[0] = vertex[1];
							tri1.vertex[1] = vertex[0];
							tri1.vertex[2] = vertex[2];
							triangleList.Add(tri1);

							Triangle tri2 = new Triangle();
							tri2.shader = shader;
							tri2.vertex = new Vertex[3];
							tri2.vertex[0] = vertex[1];
							tri2.vertex[1] = vertex[2];
							tri2.vertex[2] = vertex[3];
							triangleList.Add(tri2);

							meshBlockOffset += 8;
							polyCount --;
						}
					}
				}
			}

			triangleList.Sort();
			
			int count = triangleList.Count * 3;
			Vector3[] vertices = new Vector3[count];
			Vector3[] normals = new Vector3[count];
			Vector2[] uv = new Vector2[count];
			Color[] colors = new Color[count];
			int[] triangles = new int[count];
			int[] groups = new int[xgModel.shaders.Count];
			int[] materials = new int[xgModel.shaders.Count];
			int triangleStart = 0;
			int groupIndex = 0;
			while (triangleStart < triangleList.Count) {
				// find how many triangle share the same shader
				int shader = triangleList[triangleStart].shader;
				int triangleEnd = triangleStart+1;
				while (triangleEnd < triangleList.Count && triangleList[triangleEnd].shader == shader) {
					triangleEnd++;
				}
				int triangleCount = triangleEnd - triangleStart;
				groups[groupIndex] = triangleCount;
				materials[groupIndex] = shader;
				for (int i=0; i<triangleCount; i++) {
					int triangleIndex = triangleStart + i;
					Triangle tri = triangleList[triangleIndex];
					for (int j=0; j<3; j++) {
						Vertex vertex = tri.vertex[j];
						int idx = triangleIndex * 3 + j;
						
						vertices[idx] = new Vector3(vertex.x, vertex.y, vertex.z);
						normals[idx] = new Vector3(vertex.nx, vertex.ny, vertex.nz);
						normals[idx].Normalize();
						uv[idx] = new Vector2((float)vertex.u / 256.0f, (float)vertex.v / 256.0f);
						colors[idx] = new Color(vertex.r, vertex.g, vertex.b, vertex.a);
						triangles[idx] = idx;
					}
				}
				triangleStart = triangleEnd;
				groupIndex++;
			}
			
			XGMesh xgMesh = new XGMesh();
			xgMesh.meshes = new Mesh[groupIndex];
			xgMesh.materials = new int[groupIndex];
			
			triangleStart = 0;
			for (int i = 0; i < groupIndex; i++) {
				xgMesh.materials[i] = materials[i];
				int[] submesh = new int[groups[i] * 3];
				for(int j=0; j<groups[i] * 3; j++) {
					submesh[j] = triangles[triangleStart * 3 + j];	
				}
				Mesh mesh = new Mesh();
				mesh.name = "part" + partIndex + "_group" + groupIndex;
				mesh.vertices = vertices;
				mesh.normals = normals;
				mesh.uv = uv;
				mesh.colors = colors;
				mesh.subMeshCount = 1;
				mesh.SetTriangles(submesh, 0);	
				xgMesh.meshes[i] = mesh;
				triangleStart += groups[i];
			}
			
			xgModel.meshes.Add(xgMesh);
		}
		return xgModel;
	}

	private static uint Adler32(byte[] bytes) {
		const uint a32mod = 65521;
		uint s1 = 1, s2 = 0;
		foreach (byte b in bytes) {
			s1 = (s1 + b) % a32mod;
			s2 = (s2 + s1) % a32mod;
		}
		return ((s2 << 16) + s1);
	}
	private static uint[] crc32_tab = {
		0x00000000, 0x1db71064, 0x3b6e20c8, 0x26d930ac, 0x76dc4190, 0x6b6b51f4, 0x4db26158, 0x5005713c,
		0xedb88320, 0xf00f9344, 0xd6d6a3e8, 0xcb61b38c, 0x9b64c2b0, 0x86d3d2d4, 0xa00ae278, 0xbdbdf21c,
	};
	private static uint UpdateCRC32(byte[] buf, uint old_crc) {
		uint crc = old_crc;

		for (int n = 0; n < buf.Length; n += 1) {
			crc ^= buf[n];
			crc = (crc >> 4) ^ crc32_tab[crc & 15];
			crc = (crc >> 4) ^ crc32_tab[crc & 15];
		}

		return crc;
	}
	private static void PutUInt32BE(byte[] buf, uint offset, uint val) {
		buf[offset + 0] = (byte)((val & 0xFF000000) >> 24);
		buf[offset + 1] = (byte)((val & 0x00FF0000) >> 16);
		buf[offset + 2] = (byte)((val & 0x0000FF00) >> 8);
		buf[offset + 3] = (byte)((val & 0x000000FF) >> 0);
	}
	private static void WritePNGChunk(Stream f, byte[] identBuf, byte[] buf) {
		byte[] size = new byte[4];
		PutUInt32BE(size, 0, (uint)buf.Length);
		byte[] crcBuf = new byte[4];
		uint crc = 0xFFFFFFFF;
		crc = UpdateCRC32(identBuf, crc);
		crc = UpdateCRC32(buf, crc);
		crc ^= 0xFFFFFFFF;
		PutUInt32BE(crcBuf, 0, crc);
		f.Write(size, 0, size.Length);
		f.Write(identBuf, 0, identBuf.Length);
		f.Write(buf, 0, buf.Length);
		f.Write(crcBuf, 0, crcBuf.Length);
	}
	private static byte[] UInt32LEArrayToByteArray(uint[] inbuf) {
		byte[] outbuf = new byte[inbuf.Length * 4];
		for (var i = 0; i < inbuf.Length; i += 1) {
			uint val = inbuf[i];
			outbuf[(i * 4) + 3] = (byte)((val & 0xFF000000) >> 24);
			outbuf[(i * 4) + 2] = (byte)((val & 0x00FF0000) >> 16);
			outbuf[(i * 4) + 1] = (byte)((val & 0x0000FF00) >> 8);
			outbuf[(i * 4) + 0] = (byte)((val & 0x000000FF) >> 0);
		}
		return outbuf;
	}

	private static void WritePNG(uint[] pixels, int imageWidth, int imageHeight, string path) {
		byte[] pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
		byte[] pngIHDR = new byte[13];
		byte[] pngIDAT = new byte[0];
		byte[] pngIEND = new byte[0];
		byte[] pngChunkIdIHDR = new byte[] { 0x49, 0x48, 0x44, 0x52 };
		byte[] pngChunkIdIDAT = new byte[] { 0x49, 0x44, 0x41, 0x54 };
		byte[] pngChunkIdIEND = new byte[] { 0x49, 0x45, 0x4E, 0x44 };

		byte[] pixelsBytes = UInt32LEArrayToByteArray(pixels);

		PutUInt32BE(pngIHDR, 0, (uint)imageWidth); // width
		PutUInt32BE(pngIHDR, 4, (uint)imageHeight); // height
		pngIHDR[8] = 8; // depth
		pngIHDR[9] = 6; // color type
		pngIHDR[10] = 0; // compression
		pngIHDR[11] = 0; // filter
		pngIHDR[12] = 0; // interlace

		using (var cbio = new MemoryStream(pixelsBytes.Length)) {
			cbio.WriteByte(0x78);
			cbio.WriteByte(0x9C);
			using (var cbio2 = new MemoryStream(pixelsBytes.Length + imageHeight)) {
				for (int row = 0; row < imageHeight; row += 1) {
					cbio2.WriteByte(0x00);
					cbio2.Write(pixelsBytes, ((imageHeight - (row + 1)) * imageWidth) * 4, imageWidth * 4);
				}
				cbio2.Seek(0, SeekOrigin.Begin);
				byte[] filteredPixelsBytes = new byte[cbio2.Length];
				cbio2.Read(filteredPixelsBytes, 0, (int)cbio2.Length);
				using (var cbiod = new DeflateStream(cbio, CompressionMode.Compress, true)) {
					cbiod.Write(filteredPixelsBytes, 0, filteredPixelsBytes.Length);
				}
				byte[] adlerBuf = new byte[4];
				PutUInt32BE(adlerBuf, 0, Adler32(filteredPixelsBytes));
				cbio.Write(adlerBuf, 0, adlerBuf.Length);
			}
			cbio.Seek(0, SeekOrigin.Begin);
			pngIDAT = new byte[cbio.Length];
			cbio.Read(pngIDAT, 0, (int)cbio.Length);
		}


		using (var wf = File.Open(path, FileMode.Create)) {
			wf.Write(pngHeader, 0, pngHeader.Length);
			WritePNGChunk(wf, pngChunkIdIHDR, pngIHDR);
			WritePNGChunk(wf, pngChunkIdIDAT, pngIDAT);
			WritePNGChunk(wf, pngChunkIdIEND, pngIEND);
		}
	}

	private static uint checkTransparency(uint[] pixels) {
		uint ret = 0;

		for (int i = 0; i < pixels.Length; i += 1) {
			uint alpha = (pixels[i] & 0xFF000000) >> 24;
			if (alpha != 0xFF) {
				// Is alpha
				ret |= 1;
			}
			if (alpha != 0x00 && alpha != 0xFF) {
				// Is semi-transparent
				ret |= 2;
			}
		}

		return ret;
	}

	static uint getColour(ushort col, bool abe, int alpha) {
	    bool stp = (col & 0x8000) != 0;
	    int r = (((col     ) & 31) * 255 + 15) / 31;
	    int g = (((col >>  5) & 31) * 255 + 15) / 31;
	    int b = (((col >> 10) & 31) * 255 + 15) / 31;
		int a = 255;
	    if ((col & 0x7FFF) == 0) {
			if (stp) {
		    	a = 255;
			} else {
		    	a = 0;
			} 
		} else if (stp && abe) {
			a = alpha;
		}
		return (((uint)r & 0xFF) << 0) | (((uint)g & 0xFF) << 8) | (((uint)b & 0xFF) << 16) | (((uint)a & 0xFF) << 24);
	}

	
	static XGTexture[] importFieldTextures(byte[] textureData, List<XGShader> shaderList) {
		byte[] vram = new byte[2048 * 1024];

		// unpack MIM data into "VRAM"
		uint offset = 0;
		while (offset < textureData.Length) {
			uint header = offset;
			uint type = getUInt32LE(textureData, header + 0);
			uint pos_x = getUInt16LE(textureData, header + 4);
			uint pos_y = getUInt16LE(textureData, header + 6);
			uint move_x = getUInt16LE(textureData, header + 8); 
			uint move_y = getUInt16LE(textureData, header + 10);
			uint width = getUInt16LE(textureData, header + 12);
			uint height = getUInt16LE(textureData, header + 14);
			uint chunks = getUInt16LE(textureData, header + 18);

			uint blockSize = 0x1C + chunks * 2;
			offset += (uint)((blockSize + 2047) & ~2047);
			for(int i=0; i<chunks; i++) {
				height = getUInt16LE(textureData, header + 0x1C);
				for(int j=0; j<height; j++) {
					uint vramAddr = (uint)((pos_y + move_y + j) * 2048 + (pos_x + move_x) * 2);
					uint texAddr = (uint)(offset + j * width * 2);
					bool contains_nonzero = false;
					for (int k=0; k<(width * 2); k++) {
						if (textureData[texAddr + k] != 0) {
							contains_nonzero = true;
							break;
						}
					}
					if (contains_nonzero == false) {
						continue;
					}
					for (int k=0; k<(width * 2); k++) {
		    			vram[vramAddr++] = textureData[texAddr++];
					}
				}
			    pos_y += height;
    			blockSize = width * height * 2;
    			offset +=  (uint)((blockSize + 2047) & ~2047);
			}
		}
	
	    // convert textures with their palette
	    XGTexture[] textures = new XGTexture[shaderList.Count];
		for(int shaderIndex=0; shaderIndex < shaderList.Count; shaderIndex++) {
			XGShader shader = shaderList[shaderIndex];
			if(shader.tme) {
			    uint tx = (shader.status & 0xF) * 64 * 2;
			    uint ty = ((shader.status >> 4) & 1) * 256;
			    uint abr = (shader.status >> 5) & 3;
			    uint tp = (shader.status >> 7) & 3;
			    uint px = (shader.clut & 63) * 16;
			    uint py = shader.clut >> 6;
			    
				int alpha = 0;
				if (shader.abe) {
					switch (abr) {
					case 0: alpha = 128; break;
					case 1: alpha = 0; break;
					case 2: alpha = 0; break;
					case 3: alpha = 64; break;
					}
				}
				
	    		uint[] image = new uint[256*256];
				switch(tp) {
					case 0: { // 4-bit
						uint[] pal = new uint[16];
						for (int idx=0; idx<16; idx++) {
							uint vaddr = (uint)(py * 2048 + idx * 2 + px * 2);
							ushort col = (ushort)(vram[vaddr] + vram[vaddr+1] * 256);
							pal[idx] = getColour(col, shader.abe, alpha);
						}
						for (int y=0; y<256; y++) {
							for (int x=0; x<256; x++) {
				    			int val = vram[(y + ty) * 2048 + (x/2) + tx];
								int idx = (x & 1) != 0 ? val >> 4 : val & 0xF;
								image[y * 256 + x] = pal[idx];
							}
						}
						break;
					}
				    case 1: {
						uint[] pal = new uint[256];
						for (int idx=0; idx<256; idx++) {
							uint vaddr = (uint)(py * 2048 + idx * 2 + px * 2);
							ushort col = (ushort)(vram[vaddr] + vram[vaddr+1] * 256);
							pal[idx] = getColour(col, shader.abe, alpha);
						}
						for (int y=0; y<256; y++) {
							for (int x=0; x<256; x++) {
								uint idx = vram[(y + ty) * 2048 + x + tx];
								image[y * 256 + x] = pal[idx];
							}
						}
						break;
					}
				    case 2: {
						for (int y=0; y<256; y++) {
							for (int x=0; x<256; x++) {
							    uint vaddr = (uint)((y + ty) * 2048 + x * 2 + tx);
							    ushort col = (ushort)(vram[vaddr] + vram[vaddr+1] * 256);
								image[y * 256 + x]  = getColour(col, shader.abe, alpha);
							}
						}
						break;
					}
				}
			
				XGTexture texture = new XGTexture();
				texture.width = 256;
				texture.height = 256;
				texture.name = "texture" + shaderIndex;
				texture.pixels = image;
				textures[shaderIndex] = texture;
			} else {
				textures[shaderIndex] = null;
			}
		}
			
	    return textures;
	}

	static void wipePrefabEmbeddedAssets(string prefabPath) {
		string pathGuid = AssetDatabase.AssetPathToGUID (prefabPath);
		if (pathGuid.Length != 0)
		{
			// The following wipes out the GUID and thus prefab connections, so avoid using this
			// AssetDatabase.DeleteAsset(prefabPath);

			// Instead, delete embedded assets (not GameObject or components etc)
			UnityEngine.Object[] data = AssetDatabase.LoadAllAssetRepresentationsAtPath(prefabPath);

			foreach (UnityEngine.Object o in data)
			{
				AssetDatabase.RemoveObjectFromAsset(o);
			}
		}
	}

	static string createFolderIfNotExistent(string rootDir, string newDirName) {
		string dirGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine(rootDir, newDirName)));
		if (dirGuid.Length == 0) {
			dirGuid = AssetDatabase.CreateFolder(rootDir, newDirName);
		} 
		string newDirRoot = AssetDatabase.GUIDToAssetPath(dirGuid);
		return newDirRoot;
	}

	static void saveMeshAssets(XGModel model, string rootDir, string namePrefix) {
		int mesh_count = 0;
		foreach (XGMesh xgMesh in model.meshes) {
			for (int i = 0; i < xgMesh.meshes.Length; i += 1) {
				AssetDatabase.CreateAsset(xgMesh.meshes[i], ToUnityPath(Path.Combine(rootDir, namePrefix + "_meshitem" + mesh_count + "_mesh" + i + ".mesh")));
			}
			mesh_count += 1;
		}
	}

	static Texture2D[] saveTextureAssets(XGTexture[] textures, string rootDir, string namePrefix) {
		Texture2D[] texture2ds = new Texture2D[textures.Length];
		for(int i=0; i<textures.Length; i++) {
			if (textures[i] != null) {
				string imageFilePath = ToUnityPath(Path.Combine(rootDir, namePrefix + "_texture" + i + ".png"));
				WritePNG(textures[i].pixels, textures[i].width, textures[i].height, imageFilePath);
				AssetDatabase.ImportAsset(imageFilePath);
				Texture2D texture2d = (Texture2D)AssetDatabase.LoadMainAssetAtPath(imageFilePath);
				texture2ds[i] = texture2d;
			}
		}
		return texture2ds;
	}

	static void saveMaterialAssets(XGModel model, XGTexture[] textures, Texture2D[] texture2ds, Material[] materials, string rootDir, string namePrefix) {
		Shader opaqueShader = Shader.Find ("Diffuse");
		Shader cutoutShader = Shader.Find ("Transparent/Cutout/Diffuse");
		Shader transparentShader = Shader.Find ("Transparent/Diffuse");
		for (int i=0; i<model.shaders.Count; i++) {
			if (textures[i] != null) {
				uint transparency = checkTransparency(textures[i].pixels);
				if ((transparency & 2) != 0) { // Is semi transparent
					materials[i] = new Material(transparentShader);
				}
				else if ((transparency & 1) != 0) { // Is either fully transparent or opaque
					materials[i] = new Material(cutoutShader);
				}
				else { // Fully opaque
					materials[i] = new Material(opaqueShader);
				}
			}
			else {
				materials[i] = new Material(transparentShader);
			}
			materials[i].name = "material" + i;
			if (texture2ds[i] != null) {
				materials[i].mainTexture = texture2ds[i];
			}
			AssetDatabase.CreateAsset(materials[i], ToUnityPath(Path.Combine(rootDir, namePrefix + "_" + materials[i].name + ".mat")));
		}
	}

	static void saveAnimationAssets(AnimationClip[] animationClip, string rootDir, string namePrefix) {
		for (int i=0; i<animationClip.Length; i++) {
			AssetDatabase.CreateAsset(animationClip[i], ToUnityPath(Path.Combine(rootDir, namePrefix + "_animation" + i + ".anim")));
		}
	}

	static void saveSceneAsset(string rootDir, string namePrefix) {
		EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ToUnityPath(Path.Combine(rootDir, namePrefix + ".unity")));
	}

	static void importField(uint fileIndex, bool shouldAttachXGFieldNode) {
		string xenogearsRoot = createFolderIfNotExistent("Assets", "Xenogears");
		string stageRoot = createFolderIfNotExistent(xenogearsRoot, "Field");
		string stageModelRoot = createFolderIfNotExistent(stageRoot, "Model");
		string stageSceneRoot = createFolderIfNotExistent(stageRoot, "Scene");
		string stageMeshRoot = createFolderIfNotExistent(stageRoot, "Mesh");
		string stageTextureRoot = createFolderIfNotExistent(stageRoot, "Texture");
		string stageMaterialRoot = createFolderIfNotExistent(stageRoot, "Material");

		string namePrefix = "field" + fileIndex;

		// Create Scene
		EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
		
		uint diskIndex = 1; // there are disk 1 and disk 2
		uint dirIndex = 11; // 0-based index

		string dataPath = ToUnityPath(Path.Combine( Path.Combine(Application.dataPath, ".."), "Data"));
		string fieldPath = ToUnityPath(Path.Combine( dataPath, Path.Combine( "disk" + diskIndex, "dir" + dirIndex)));
		string archivePath = ToUnityPath(Path.Combine( fieldPath, "file" + (fileIndex * 2) + ".bin"));
		string texturePath = ToUnityPath(Path.Combine( fieldPath, "file" + (fileIndex * 2 + 1) + ".bin"));

		byte[] archiveData = File.ReadAllBytes(archivePath);
		byte[] modelData = getData(archiveData, 2);
		byte[] textureData = File.ReadAllBytes(texturePath);
		
		string prefabPath = ToUnityPath(Path.Combine(stageModelRoot, namePrefix + ".prefab"));

		XGModel model = importFieldModel(modelData);
		saveMeshAssets(model, stageMeshRoot, namePrefix);

		XGTexture[] textures = importFieldTextures(textureData, model.shaders);
		Texture2D[] texture2ds = saveTextureAssets(textures, stageTextureRoot, namePrefix);
		
		Material[] materials = new Material[model.shaders.Count];
		saveMaterialAssets(model, textures, texture2ds, materials, stageMaterialRoot, namePrefix);
		
		GameObject gameObject = new GameObject(namePrefix);
		//gameObject.transform.localScale = new Vector3(-1,1,1);
		int itemCount = (int)getUInt32LE(archiveData, 0x018C);
		for(uint itemIndex=0; itemIndex < itemCount; itemIndex++) {
			ushort flags = getUInt16LE(archiveData, 0x0190 + itemIndex * 16 + 0);
			ushort rot_x = getUInt16LE(archiveData, 0x0190 + itemIndex * 16 + 2);
			ushort rot_y = getUInt16LE(archiveData, 0x0190 + itemIndex * 16 + 4);
			ushort rot_z = getUInt16LE(archiveData, 0x0190 + itemIndex * 16 + 6);
			short pos_x = (short)getUInt16LE(archiveData, 0x0190 + itemIndex * 16 + 8);
			short pos_y = (short)getUInt16LE(archiveData, 0x0190 + itemIndex * 16 + 10);
			short pos_z = (short)getUInt16LE(archiveData, 0x0190 + itemIndex * 16 + 12);
			ushort index = getUInt16LE(archiveData, 0x0190 + itemIndex * 16 + 14);
			
			var itemName = namePrefix + "_item" + itemIndex;
			Transform item = gameObject.transform.Find(itemName);
			if (item == null) {
				item = new GameObject(itemName).transform;
			}
			if (item.parent != gameObject.transform) {
				item.SetParent(gameObject.transform);
			}
			if (shouldAttachXGFieldNode) {
				XGFieldNode xgFieldNode = item.gameObject.GetComponent<XGFieldNode>();
				if (xgFieldNode == null) {
					xgFieldNode = (XGFieldNode)item.gameObject.AddComponent (typeof(XGFieldNode));
				}
				xgFieldNode.flag = new bool[16];
				for(int i=0; i<16; i++) {
					xgFieldNode.flag[i] = (flags & (1<<i)) != 0;
				}
				xgFieldNode.index = index;
			}
			if ((flags & ((1<<5)|(1<<6)/*|(1<<7)|(1<<8)*/)) == 0) {
				XGMesh xgMesh = model.meshes[index]; 
				for (int ii = 0; ii < xgMesh.meshes.Length; ii += 1) {
					var meshNodeName = namePrefix + "_item" + itemIndex + "_mesh" + ii;
					Transform meshNode = item.Find(meshNodeName);
					if (meshNode == null) {
						meshNode = new GameObject(meshNodeName).transform;
					}
					if (meshNode.parent != item) {
						meshNode.SetParent(item);
					}
					MeshFilter meshFilter = meshNode.gameObject.GetComponent<MeshFilter>();
					if (meshFilter == null) {
						meshFilter = (MeshFilter)meshNode.gameObject.AddComponent(typeof(MeshFilter));
					}
					MeshRenderer renderer = meshNode.gameObject.GetComponent<MeshRenderer>();
					if (renderer == null) {
						renderer = (MeshRenderer)meshNode.gameObject.AddComponent(typeof(MeshRenderer));
					}
					meshFilter.mesh = xgMesh.meshes[ii];
					Material[] meshMaterials = new Material[1];
					meshMaterials[0] = materials[xgMesh.materials[ii]];
					renderer.materials = meshMaterials;
				}
			}
			item.transform.localPosition = new Vector3(pos_x, pos_y, pos_z);
			// Unity's euler angle to quaternion code is not giving the correct results, so do the calculation ourselves
			float[] quat = new float[] {rot_x * 90.0f / 1024.0f, rot_y * 90.0f / 1024.0f, rot_z * 90.0f / 1024.0f, 0.0f};
			arr_deg_to_quat(quat);
			// Unity's Quaternion constructor is in xyzw order (not wxyz)
			item.transform.localRotation = new Quaternion(quat[1], quat[2], quat[3], quat[0]);
		}
		gameObject.transform.localScale = new Vector3(0.02f,0.02f,-0.02f);
		gameObject.transform.localEulerAngles = new Vector3(-180,90,0);

		wipePrefabEmbeddedAssets(prefabPath);
		PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, prefabPath, InteractionMode.UserAction);

		RenderSettings.ambientLight = Color.white;
		
		saveSceneAsset(stageSceneRoot, namePrefix);
	}
	
	static XGModel importStageModel(byte[] data) {
		XGModel xgModel = new XGModel();
		xgModel.meshes = new List<XGMesh>();
		xgModel.shaders = new List<XGShader>();

		uint modelOffset = getUInt32LE(data, 8);
		uint blockCount = getUInt32LE(data, modelOffset + 0);
		for (int blockIndex=0; blockIndex<blockCount; blockIndex++) {
			List<Triangle> triangleList = new List<Triangle>();
			
			uint blockOffset = (uint)(modelOffset + 16 + blockIndex * 0x38);
			ushort vertexCount = getUInt16LE(data, blockOffset + 2);
			ushort meshCount = getUInt16LE(data, blockOffset + 4);
			ushort number_mesh_block = getUInt16LE(data, blockOffset + 6);
			
			uint vertexOffset = modelOffset + getUInt32LE(data, blockOffset + 8);
			uint meshBlockOffset = modelOffset + getUInt32LE(data, blockOffset + 16);
			uint normalOffset = modelOffset + getUInt32LE(data, blockOffset + 12);
			uint displayListOffset = modelOffset + getUInt32LE(data, blockOffset + 20);
			
			int polyCount = 0;
			
			bool quad_block = false;
			uint status = 0;
			uint clut = 0;
			while (polyCount > 0 || number_mesh_block > 0) {
				// init the mesh block
				if (polyCount == 0) {
					quad_block = (data[meshBlockOffset + 0] & 16) != 0;
					polyCount = getUInt16LE(data, meshBlockOffset + 2);
					meshBlockOffset+=4;
					number_mesh_block--;
				}
	
				// decode command
				uint cmd = getUInt32LE(data, displayListOffset);
	    
				bool hp = ((cmd >> 24) & 16) != 0;	    // geraud shading
				bool quad = ((cmd >> 24) & 8) != 0;	    // quad or tri
				bool tme = ((cmd >> 24) & 4) != 0;	    // texture mapping
				bool abe = ((cmd >> 24) & 2) != 0;	    // semi transparency
				bool fullbright = ((cmd >> 24) & 1) != 0;	    // bypass lighting
				uint op = (cmd >> 24) & 255;		    // operator
				uint pop = (uint)(op & ~(16|2|1));		    // operator, with shading and lighting mask
				
				displayListOffset += 4;
	    		if (op == 0xC4) { // texture page
					status = cmd & 0xFFFF;
				} else if (op == 0xC8) { // clut
					clut = cmd & 0xFFFF;
				} else if (pop == 0x24) { // triangle with texture	
					int ua = data[displayListOffset + 0];
					int va = data[displayListOffset + 1];
					int ub = data[displayListOffset + 2];
					int vb = data[displayListOffset + 3];
					int uc = (int)(cmd & 255);
					int vc = (int)((cmd >> 8) & 255);
					displayListOffset += 4;

					int shader = addShader(xgModel.shaders, new XGShader(status, clut, abe, true));
					Vertex[] vertex = new Vertex[3];
					for(int j=0; j<3; j++) {
						vertex[j] = new Vertex();
					}
					vertex[0].u = ua;
					vertex[0].v = va;
					vertex[1].u = ub;
					vertex[1].v = vb;
					vertex[2].u = uc;
					vertex[2].v = vc;
					Vector3[] vec = new Vector3[3];
					for(uint j=0; j<3; j++) {
					    uint vtx = getUInt16LE(data, meshBlockOffset + j * 2);
						vertex[j].x = (short)getUInt16LE(data, vertexOffset + vtx * 8 + 0);
						vertex[j].y = (short)getUInt16LE(data, vertexOffset + vtx * 8 + 2);
						vertex[j].z = (short)getUInt16LE(data, vertexOffset + vtx * 8 + 4);
					    if (hp) {
							vertex[j].nx = (short)getUInt16LE(data, normalOffset + vtx * 8 + 0);
							vertex[j].ny = (short)getUInt16LE(data, normalOffset + vtx * 8 + 2);
							vertex[j].nz = (short)getUInt16LE(data, normalOffset + vtx * 8 + 4);
						} else {
							vec[j].x = vertex[j].x;
							vec[j].y = vertex[j].y;
							vec[j].z = vertex[j].z;
						}
						vertex[j].r = 255;
						vertex[j].g = 255;
						vertex[j].b = 255;
						vertex[j].a = 255;
					}
					if (!hp) {
					    Vector3 d1 = vec[2] - vec[0];
					    Vector3 d2 = vec[1] - vec[0];
					    Vector3 n = (Vector3.Cross(d1, d2)).normalized;
						n.x *= 4096.0f;
						n.y *= 4096.0f;
						n.z *= 4096.0f;
						n.x += 0.5f;
						n.y += 0.5f;
						n.z += 0.5f;
						for(uint j=0; j<3; j++) {
							vertex[j].nx = (int)(n.x);
							vertex[j].ny = (int)(n.y);
							vertex[j].nz = (int)(n.z);
						}
					}

					Triangle tri = new Triangle();
					tri.shader = shader;
					tri.vertex = new Vertex[3];
					tri.vertex[0] = vertex[1];
					tri.vertex[1] = vertex[0];
					tri.vertex[2] = vertex[2];
					triangleList.Add(tri);
					
					meshBlockOffset += 8;
					polyCount --;
				} else if ( pop == 0x2C ) { // quad with texture
					int ua = data[displayListOffset + 0];
					int va = data[displayListOffset + 1];
					int ub = data[displayListOffset + 2];
					int vb = data[displayListOffset + 3];
					int uc = data[displayListOffset + 4];
					int vc = data[displayListOffset + 5];
					int ud = data[displayListOffset + 6];
					int vd = data[displayListOffset + 7];
					displayListOffset += 8;
		
					int shader = addShader(xgModel.shaders, new XGShader(status, clut, abe, true));

					Vertex[] vertex = new Vertex[4];
					for(int j=0; j<4; j++) {
						vertex[j] = new Vertex();
					}
					vertex[0].u = ua;
					vertex[0].v = va;
					vertex[1].u = ub;
					vertex[1].v = vb;
					vertex[2].u = uc;
					vertex[2].v = vc;
					vertex[3].u = ud;
					vertex[3].v = vd;
					Vector3[] vec = new Vector3[4];
					for(uint j=0; j<4; j++) {
						uint vtx = getUInt16LE(data, meshBlockOffset + j * 2);
						vertex[j].x = (short)getUInt16LE(data, vertexOffset + vtx * 8 + 0);
						vertex[j].y = (short)getUInt16LE(data, vertexOffset + vtx * 8 + 2);
						vertex[j].z = (short)getUInt16LE(data, vertexOffset + vtx * 8 + 4);
					    if (hp) {
							vertex[j].nx = (short)getUInt16LE(data, normalOffset + vtx * 8 + 0);
							vertex[j].ny = (short)getUInt16LE(data, normalOffset + vtx * 8 + 2);
							vertex[j].nz = (short)getUInt16LE(data, normalOffset + vtx * 8 + 4);
						} else {
							vec[j].x = vertex[j].x;
							vec[j].y = vertex[j].y;
							vec[j].z = vertex[j].z;
						}
						vertex[j].r = 255;
						vertex[j].g = 255;
						vertex[j].b = 255;
						vertex[j].a = 255;
					}
					if (!hp) {
					    Vector3 d1 = vec[2] - vec[0];
					    Vector3 d2 = vec[1] - vec[0];
					    Vector3 n = (Vector3.Cross(d1, d2)).normalized;
						n.x *= 4096.0f;
						n.y *= 4096.0f;
						n.z *= 4096.0f;
						n.x += 0.5f;
						n.y += 0.5f;
						n.z += 0.5f;
						
						for(uint j=0; j<4; j++) {
							vertex[j].nx = (int)(n.x);
							vertex[j].ny = (int)(n.y);
							vertex[j].nz = (int)(n.z);
						}
					}
					
					Triangle tri1 = new Triangle();
					tri1.shader = shader;
					tri1.vertex = new Vertex[3];
					tri1.vertex[0] = vertex[1];
					tri1.vertex[1] = vertex[0];
					tri1.vertex[2] = vertex[2];
					triangleList.Add(tri1);

					Triangle tri2 = new Triangle();
					tri2.shader = shader;
					tri2.vertex = new Vertex[3];
					tri2.vertex[0] = vertex[1];
					tri2.vertex[1] = vertex[2];
					tri2.vertex[2] = vertex[3];
					triangleList.Add(tri2);
					
					meshBlockOffset += 8;
					polyCount --;
				} else if (pop == 0x20) { // monochrome triangle
					uint r = (cmd >> 16) & 255;
					uint g = (cmd >> 8) & 255;
					uint b = (cmd) & 255;
					uint a = 255;
					if (abe) {
					    switch ((status >> 5) & 3) {
						case 0: a = 128; break;
						case 1: a = 0; break;
						case 2: a = 0; break;
						case 3: a = 64; break;
						}
					}
		
					int shader = addShader(xgModel.shaders, new XGShader(status, clut, abe, false));
					Vertex[] vertex = new Vertex[3];
					Vector3[] vec = new Vector3[3];
					for(uint j=0; j<3; j++) {
						vertex[j] = new Vertex();
						vertex[j].u = 0;
						vertex[j].v = 0;
						uint vtx = getUInt16LE(data, meshBlockOffset + j * 2);
						vertex[j].x = (short)getUInt16LE(data, vertexOffset + vtx * 8 + 0);
						vertex[j].y = (short)getUInt16LE(data, vertexOffset + vtx * 8 + 2);
						vertex[j].z = (short)getUInt16LE(data, vertexOffset + vtx * 8 + 4);
					    if (hp) {
							vertex[j].nx = (short)getUInt16LE(data, normalOffset + vtx * 8 + 0);
							vertex[j].ny = (short)getUInt16LE(data, normalOffset + vtx * 8 + 2);
							vertex[j].nz = (short)getUInt16LE(data, normalOffset + vtx * 8 + 4);
						} else {
							vec[j].x = vertex[j].x;
							vec[j].y = vertex[j].y;
							vec[j].z = vertex[j].z;
						}
						vertex[j].r = r;
						vertex[j].g = g;
						vertex[j].b = b;
						vertex[j].a = a;
					}
					if (!hp) {
					    Vector3 d1 = vec[2] - vec[0];
					    Vector3 d2 = vec[1] - vec[0];
					    Vector3 n = (Vector3.Cross(d1, d2)).normalized;
						n.x *= 4096.0f;
						n.y *= 4096.0f;
						n.z *= 4096.0f;
						n.x += 0.5f;
						n.y += 0.5f;
						n.z += 0.5f;
						for(uint j=0; j<3; j++) {
							vertex[j].nx = (int)(n.x);
							vertex[j].ny = (int)(n.y);
							vertex[j].nz = (int)(n.z);
						}
					}
										
					Triangle tri = new Triangle();
					tri.shader = shader;
					tri.vertex = new Vertex[3];
					tri.vertex[0] = vertex[1];
					tri.vertex[1] = vertex[0];
					tri.vertex[2] = vertex[2];
					triangleList.Add(tri);
					
					meshBlockOffset += 8;
					polyCount --;
				} else if (pop == 0x28) { // monochrome quad
					uint r = (cmd >> 16) & 255;
					uint g = (cmd >> 8) & 255;
					uint b = (cmd) & 255;
					uint a = 255;
					if (abe) {
					    switch ((status >> 5) & 3) {
						case 0: a = 128; break;
						case 1: a = 0; break;
						case 2: a = 0; break;
						case 3: a = 64; break;
						}
					}
		
					int shader = addShader(xgModel.shaders, new XGShader(status, clut, abe, false));
					Vertex[] vertex = new Vertex[4];
					Vector3[] vec = new Vector3[4];
					for(uint j=0; j<4; j++) {
						vertex[j] = new Vertex();
						vertex[j].u = 0;
						vertex[j].v = 0;
						uint vtx = getUInt16LE(data, meshBlockOffset + j * 2);
						vertex[j].x = (short)getUInt16LE(data, vertexOffset + vtx * 8 + 0);
						vertex[j].y = (short)getUInt16LE(data, vertexOffset + vtx * 8 + 2);
						vertex[j].z = (short)getUInt16LE(data, vertexOffset + vtx * 8 + 4);
					    if (hp) {
							vertex[j].nx = (short)getUInt16LE(data, normalOffset + vtx * 8 + 0);
							vertex[j].ny = (short)getUInt16LE(data, normalOffset + vtx * 8 + 2);
							vertex[j].nz = (short)getUInt16LE(data, normalOffset + vtx * 8 + 4);
						} else {
							vec[j].x = vertex[j].x;
							vec[j].y = vertex[j].y;
							vec[j].z = vertex[j].z;
						}
						vertex[j].r = r;
						vertex[j].g = g;
						vertex[j].b = b;
						vertex[j].a = a;
					}
					if (!hp) {
					    Vector3 d1 = vec[2] - vec[0];
					    Vector3 d2 = vec[1] - vec[0];
					    Vector3 n = (Vector3.Cross(d1, d2)).normalized;
						n.x *= 4096.0f;
						n.y *= 4096.0f;
						n.z *= 4096.0f;
						n.x += 0.5f;
						n.y += 0.5f;
						n.z += 0.5f;
						for(uint j=0; j<4; j++) {
							vertex[j].nx = (int)(n.x);
							vertex[j].ny = (int)(n.y);
							vertex[j].nz = (int)(n.z);
						}
					}
					
					Triangle tri1 = new Triangle();
					tri1.shader = shader;
					tri1.vertex = new Vertex[3];
					tri1.vertex[0] = vertex[1];
					tri1.vertex[1] = vertex[0];
					tri1.vertex[2] = vertex[2];
					triangleList.Add(tri1);

					Triangle tri2 = new Triangle();
					tri2.shader = shader;
					tri2.vertex = new Vertex[3];
					tri2.vertex[0] = vertex[1];
					tri2.vertex[1] = vertex[2];
					tri2.vertex[2] = vertex[3];
					triangleList.Add(tri2);

					meshBlockOffset += 8;
					polyCount --;
				}
			}

			triangleList.Sort();
	
			int count = triangleList.Count * 3;
			Vector3[] vertices = new Vector3[count];
			Vector3[] normals = new Vector3[count];
			Vector2[] uv = new Vector2[count];
			Color[] colors = new Color[count];
			int[] triangles = new int[count];
			int[] groups = new int[xgModel.shaders.Count];
			int[] materials = new int[xgModel.shaders.Count];
			int triangleStart = 0;
			int groupIndex = 0;
			while (triangleStart < triangleList.Count) {
				// find how many triangle share the same shader
				int shader = triangleList[triangleStart].shader;
				int triangleEnd = triangleStart+1;
				while (triangleEnd < triangleList.Count && triangleList[triangleEnd].shader == shader) {
					triangleEnd++;
				}
				int triangleCount = triangleEnd - triangleStart;
				groups[groupIndex] = triangleCount;
				materials[groupIndex] = shader;
				for (int i=0; i<triangleCount; i++) {
					int triangleIndex = triangleStart + i;
					Triangle tri = triangleList[triangleIndex];
					for (int j=0; j<3; j++) {
						Vertex vertex = tri.vertex[j];
						int idx = triangleIndex * 3 + j;
						
						vertices[idx] = new Vector3(vertex.x, vertex.y, vertex.z);
						normals[idx] = new Vector3(vertex.nx, vertex.ny, vertex.nz);
						normals[idx].Normalize();
						uv[idx] = new Vector2((float)vertex.u / 256.0f, (float)vertex.v / 256.0f);
						colors[idx] = new Color(vertex.r, vertex.g, vertex.b, vertex.a);
						triangles[idx] = idx;
					}
				}
				triangleStart = triangleEnd;
				groupIndex++;
			}
			
			XGMesh xgMesh = new XGMesh();
			xgMesh.meshes = new Mesh[groupIndex];
			xgMesh.materials = new int[groupIndex];
			
			triangleStart = 0;
			for (int i = 0; i < groupIndex; i++) {
				xgMesh.materials[i] = materials[i];
				int[] submesh = new int[groups[i] * 3];
				for(int j=0; j<groups[i] * 3; j++) {
					submesh[j] = triangles[triangleStart * 3 + j];	
				}
				Mesh mesh = new Mesh();
				mesh.name = "part" + blockIndex + "_group" + groupIndex;
				mesh.vertices = vertices;
				mesh.normals = normals;
				mesh.uv = uv;
				mesh.colors = colors;
				mesh.subMeshCount = 1;
				mesh.SetTriangles(submesh, 0);	
				xgMesh.meshes[i] = mesh;
				triangleStart += groups[i];
			}
			
			xgModel.meshes.Add(xgMesh);
		}
		
		return xgModel;
	}
	
	static XGTexture[] importStageTextures(byte[] textureData, List<XGShader> shaderList) {
		byte[] vram = new byte[2048 * 1024];

		// unpack MIM data into "VRAM"
		uint offset = getUInt32LE(textureData, 4);
		uint num_textures = getUInt32LE(textureData, offset);
		for (uint textureIndex=0; textureIndex<num_textures; textureIndex++) {
			uint header = offset + getUInt32LE(textureData, offset + 4 + textureIndex * 4);
			uint type = getUInt32LE(textureData, header + 0);
			uint pos_x = getUInt16LE(textureData, header + 4);
			uint pos_y = getUInt16LE(textureData, header + 6);
			uint move_x = getUInt16LE(textureData, header + 8); 
			uint move_y = getUInt16LE(textureData, header + 10);
			uint width = getUInt16LE(textureData, header + 12);
			uint height = getUInt16LE(textureData, header + 14);
			for (int j=0; j < height; j++) {
				uint vramPos = (uint)((pos_y + move_y + j) * 2048 + (pos_x + move_x) * 2);
				uint dataPos = (uint)(header + 16 + j * width * 2);
				bool contains_nonzero = false;
				for(int k=0; k<width*2; k++) {
					if (textureData[dataPos + k] != 0) {
						contains_nonzero = true;
						break;
					}
				}
				if (contains_nonzero == false) {
					continue;
				}
				for(int k=0; k<width*2; k++) {
					vram[vramPos++] = textureData[dataPos++]; 
				}
			}
		}
	
	    // convert textures with their palette
	    XGTexture[] textures = new XGTexture[shaderList.Count];
		for(int shaderIndex=0; shaderIndex < shaderList.Count; shaderIndex++) {
			XGShader shader = shaderList[shaderIndex];
			if(shader.tme) {
			    uint tx = (shader.status & 0xF) * 64 * 2;
			    uint ty = ((shader.status >> 4) & 1) * 256;
			    uint abr = (shader.status >> 5) & 3;
			    uint tp = (shader.status >> 7) & 3;
			    uint px = (shader.clut & 63) * 16;
			    uint py = shader.clut >> 6;
			    
				int alpha = 0;
				if (shader.abe) {
					switch (abr) {
					case 0: alpha = 128; break;
					case 1: alpha = 0; break;
					case 2: alpha = 0; break;
					case 3: alpha = 64; break;
					}
				}
				
	    		uint[] image = new uint[256*256];
				switch(tp) {
					case 0: { // 4-bit
						uint[] pal = new uint[16];
						for (int idx=0; idx<16; idx++) {
							uint vaddr = (uint)(py * 2048 + idx * 2 + px * 2);
							ushort col = (ushort)(vram[vaddr] + vram[vaddr+1] * 256);
							pal[idx] = getColour(col, shader.abe, alpha);
						}
						for (int y=0; y<256; y++) {
							for (int x=0; x<256; x++) {
				    			int val = vram[(y + ty) * 2048 + (x/2) + tx];
								int idx = (x & 1) != 0 ? val >> 4 : val & 0xF;
								image[y * 256 + x] = pal[idx];
							}
						}
						break;
					}
				    case 1: {
						uint[] pal = new uint[256];
						for (int idx=0; idx<256; idx++) {
							uint vaddr = (uint)(py * 2048 + idx * 2 + px * 2);
							ushort col = (ushort)(vram[vaddr] + vram[vaddr+1] * 256);
							pal[idx] = getColour(col, shader.abe, alpha);
						}
						for (int y=0; y<256; y++) {
							for (int x=0; x<256; x++) {
								uint idx = vram[(y + ty) * 2048 + x + tx];
								image[y * 256 + x] = pal[idx];
							}
						}
						break;
					}
				    case 2: {
						for (int y=0; y<256; y++) {
							for (int x=0; x<256; x++) {
							    uint vaddr = (uint)((y + ty) * 2048 + x * 2 + tx);
							    ushort col = (ushort)(vram[vaddr] + vram[vaddr+1] * 256);
								image[y * 256 + x]  = getColour(col, shader.abe, alpha);
							}
						}
						break;
					}
				}
			
				XGTexture texture = new XGTexture();
				texture.width = 256;
				texture.height = 256;
				texture.name = "texture" + shaderIndex;
				texture.pixels = image;
				textures[shaderIndex] = texture;
			} else {
				textures[shaderIndex] = null;
			}
		}
			
	    return textures;
	}
	
	static AnimationClip[] importAnimationClips(GameObject[] items, byte[] anim)
	{
		uint ofs1 = getUInt32LE (anim, 4);
		uint ofs2 = ofs1 + getUInt32LE (anim, ofs1 + 4);
		uint count3 = getUInt32LE (anim, ofs2);
		uint ofs3 = ofs2 + getUInt32LE (anim, ofs2 + 4);

		// Get first frame of first animation as default position
		uint numBones = getUInt16LE(anim, ofs3);
		// Debug.Log("NumBones:" + numBones);
		for(uint i=0; i<numBones; i++) {
			short rotX = (short)getUInt16LE(anim, ofs3 + 0x18 + i * 12 + 0);
			short rotY = (short)getUInt16LE(anim, ofs3 + 0x18 + i * 12 + 2);
			short rotZ = (short)getUInt16LE(anim, ofs3 + 0x18 + i * 12 + 4);
			short transX = (short)getUInt16LE(anim, ofs3 + 0x18 + i * 12 + 6);
			short transY = (short)getUInt16LE(anim, ofs3 + 0x18 + i * 12 + 8);
			short transZ = (short)getUInt16LE(anim, ofs3 + 0x18 + i * 12 + 10);
			
			items[i].transform.localPosition = new Vector3((float)transX, (float)transY, (float)transZ);
			float[] quat = new float[] {(float)rotX * 360.0f / 4096.0f, (float)rotY * 360.0f / 4096.0f, (float)rotZ * 360.0f / 4096.0f, 0.0f};
			arr_deg_to_quat(quat);
			items[i].transform.localRotation = new Quaternion(quat[1], quat[2], quat[3], quat[0]);
		}

		AnimationClip[] animationClip = new AnimationClip[count3];
		
		// get animations
		for(uint i=0; i<count3; i++) {
			uint ofs4 = ofs2 + getUInt32LE (anim, ofs2 + 4 + i * 4);
			uint siz4 = getUInt32LE (anim, ofs2 + 8 + i * 4) - getUInt32LE (anim, ofs2 + 4 + i * 4);
			numBones = getUInt16LE(anim, ofs4);

			ushort flags = getUInt16LE(anim, ofs4 + 0x04);
			ushort flags2 = getUInt16LE(anim, ofs4 + 0x06);
			ushort rotCount = getUInt16LE(anim, ofs4 + 0x0C);
			ushort transCount = getUInt16LE(anim, ofs4 + 0x0E);

			bool rotFlag = (flags & 0x01) == 0;
			bool transFlag = (flags & 0x02) == 0;
			uint numFrames = (uint)(((siz4 - (0x18 + (flags2 == 0 ? ((rotCount + 1) * 6) : 0))) / ((rotFlag ? 6 : 0) + (transFlag ? 6 : 0))) / numBones);

			uint ofs5 = ofs4 + 0x18;
			if (flags2 == 0) {
				ofs5 += (uint)((rotCount + 1) * 6);
			}

			if (i == 0) {
				ushort[] test = new ushort[9];
				string line = "line:";
				for(uint j=1; j<9; j++) {
					test[j] = getUInt16LE(anim, ofs4 + j * 2);
					line += " " + test[j];
				}
				Debug.Log (line);
				Debug.Log ("size:" + siz4);
				Debug.Log ("flags:"+flags);
				Debug.Log ("flags2:"+flags2);
				Debug.Log ("rotCount:"+rotCount);
				Debug.Log ("transCount:"+transCount);
				Debug.Log ("numFrames:"+ numFrames);
			}

			AnimationClip clip = new AnimationClip();
			clip.name = "animation" + i;
			clip.frameRate = 1;

			clip.legacy = false;

			uint time;

			AnimationCurve[] curveTransX = new AnimationCurve[numBones];
			AnimationCurve[] curveTransY = new AnimationCurve[numBones];
			AnimationCurve[] curveTransZ = new AnimationCurve[numBones];

			AnimationCurve[] curveRotX = new AnimationCurve[numBones];
			AnimationCurve[] curveRotY = new AnimationCurve[numBones];
			AnimationCurve[] curveRotZ = new AnimationCurve[numBones];
			AnimationCurve[] curveRotW = new AnimationCurve[numBones];

			for (uint i2 = 0; i2 < numBones; i2 += 1) {
				curveTransX[i2] = new AnimationCurve();
				curveTransY[i2] = new AnimationCurve();
				curveTransZ[i2] = new AnimationCurve();
				curveRotX[i2] = new AnimationCurve();
				curveRotY[i2] = new AnimationCurve();
				curveRotZ[i2] = new AnimationCurve();
				curveRotW[i2] = new AnimationCurve();
			}

			time = 0;
			for (uint i2 = 0; i2 < numFrames; i2 += 1) {
				ushort rotNum = 0;
				ushort transNum = 0;
				for (uint i3 = 0; i3 < numBones; i3 += 1) {
					short tx, ty, tz;
					short rx, ry, rz;
					tx = 0;
					ty = 0;
					tz = 0;
					rx = 0;
					ry = 0;
					rz = 0;

					if (rotFlag && (rotNum < transCount)) {
						rx = (short)getUInt16LE(anim, ofs5 + 0x00);
						ry = (short)getUInt16LE(anim, ofs5 + 0x02);
						rz = (short)getUInt16LE(anim, ofs5 + 0x04);
						float[] quat = new float[] {(float)rx * 360.0f / 4096.0f, (float)ry * 360.0f / 4096.0f, (float)rz * 360.0f / 4096.0f, 0.0f};
						arr_deg_to_quat(quat);
						curveRotX[i3].AddKey(new Keyframe(time, (float)(quat[1]), 0, 0, 0, 0));
						curveRotY[i3].AddKey(new Keyframe(time, (float)(quat[2]), 0, 0, 0, 0));
						curveRotZ[i3].AddKey(new Keyframe(time, (float)(quat[3]), 0, 0, 0, 0));
						curveRotW[i3].AddKey(new Keyframe(time, (float)(quat[0]), 0, 0, 0, 0));
						ofs5 += 0x6;
						rotNum += 1;
					}

					if (transFlag && (transNum < rotCount)) {
						tx = (short)getUInt16LE(anim, ofs5 + 0x00);
						ty = (short)getUInt16LE(anim, ofs5 + 0x02);
						tz = (short)getUInt16LE(anim, ofs5 + 0x04);
						curveTransX[i3].AddKey(new Keyframe(time, (float)tx, 0, 0, 0, 0));
						curveTransY[i3].AddKey(new Keyframe(time, (float)ty, 0, 0, 0, 0));
						curveTransZ[i3].AddKey(new Keyframe(time, (float)tz, 0, 0, 0, 0));
						ofs5 += 0x6;
						transNum += 1;
					}
				}

				time += 1;
			}

			for (uint i2 = 0; i2 < numBones; i2 += 1) {
				if (transFlag) {
					clip.SetCurve(items[i2].name, typeof(Transform), "localPosition.x", curveTransX[i2]);
					clip.SetCurve(items[i2].name, typeof(Transform), "localPosition.y", curveTransY[i2]);
					clip.SetCurve(items[i2].name, typeof(Transform), "localPosition.z", curveTransZ[i2]);
				}
				if (rotFlag) {
					clip.SetCurve(items[i2].name, typeof(Transform), "localRotation.x", curveRotX[i2]);
					clip.SetCurve(items[i2].name, typeof(Transform), "localRotation.y", curveRotY[i2]);
					clip.SetCurve(items[i2].name, typeof(Transform), "localRotation.z", curveRotZ[i2]);
					clip.SetCurve(items[i2].name, typeof(Transform), "localRotation.w", curveRotW[i2]);
				}
			}

			animationClip[i] = clip;
		}
		
		return animationClip;
	}
	
	static void importSceneModel(uint fileIndex) {
		string xenogearsRoot = createFolderIfNotExistent("Assets", "Xenogears");
		string stageRoot = createFolderIfNotExistent(xenogearsRoot, "SceneModel");
		string stageModelRoot = createFolderIfNotExistent(stageRoot, "Model");
		string stageSceneRoot = createFolderIfNotExistent(stageRoot, "Scene");
		string stageMeshRoot = createFolderIfNotExistent(stageRoot, "Mesh");
		string stageTextureRoot = createFolderIfNotExistent(stageRoot, "Texture");
		string stageMaterialRoot = createFolderIfNotExistent(stageRoot, "Material");
		string stageAnimationRoot = createFolderIfNotExistent(stageRoot, "Animation");

		string namePrefix = "scenemodel" + fileIndex;

		uint diskIndex = 1; // there are disk 1 and disk 2
		uint dirIndex = 12; // 0-based index
			
		string dataPath = ToUnityPath(Path.Combine( Path.Combine(Application.dataPath, ".."), "Data"));
		string stagePath = ToUnityPath(Path.Combine( dataPath, Path.Combine( "disk" + diskIndex, "dir" + dirIndex)));
		string filePath = ToUnityPath(Path.Combine( stagePath, "file" + (fileIndex * 2 + 2) + ".bin"));
		string animPath = ToUnityPath(Path.Combine( stagePath, "file" + (fileIndex * 2 + 1) + ".bin"));

		EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
		
		byte[] data = File.ReadAllBytes(filePath);
		byte[] anim = File.ReadAllBytes(animPath);

		string prefabPath = ToUnityPath(Path.Combine(stageModelRoot, namePrefix + ".prefab"));

		XGModel model = importStageModel(data);	
		saveMeshAssets(model, stageMeshRoot, namePrefix);

		XGTexture[] textures = importStageTextures(data, model.shaders);
		Texture2D[] texture2ds = saveTextureAssets(textures, stageTextureRoot, namePrefix);

		Material[] materials = new Material[model.shaders.Count];
		saveMaterialAssets(model, textures, texture2ds, materials, stageMaterialRoot, namePrefix);
		
		List<int> hierarchy = new List<int>();
		uint offset = getUInt32LE(data, 12);
		short block = (short)getUInt16LE(data, offset);
		short parent = (short)getUInt16LE(data, offset + 2);
		offset += 4;
		while (block != -2) {
			// Debug.Log("block:"+block+" parent:"+parent);
			hierarchy.Add(block);
			hierarchy.Add(parent);
			block = (short)getUInt16LE(data, offset);
			parent = (short)getUInt16LE(data, offset + 2);
			offset += 4;
		}
		
		GameObject[] items = new GameObject[hierarchy.Count / 2];
		
		GameObject gameObject = new GameObject(namePrefix);

		for(int itemIndex=0; itemIndex < items.Length; itemIndex++) {
			var itemName = namePrefix + "_item" + itemIndex;
			Transform item = gameObject.transform.Find(itemName);
			if (item == null) {
				item = new GameObject(itemName).transform;
			}
			int blockIndex = hierarchy[itemIndex*2+0];
			if (blockIndex >= 0) {
				XGMesh xgMesh = model.meshes[blockIndex]; 
				for (int ii = 0; ii < xgMesh.meshes.Length; ii += 1) {
					var meshNodeName = namePrefix + "_item" + itemIndex + "_mesh" + ii;
					Transform meshNode = item.Find(meshNodeName);
					if (meshNode == null) {
						meshNode = new GameObject(meshNodeName).transform;
					}
					meshNode.parent = item;
					MeshFilter meshFilter = meshNode.gameObject.GetComponent<MeshFilter>();
					if (meshFilter == null) {
						meshFilter = (MeshFilter)meshNode.gameObject.AddComponent(typeof(MeshFilter));
					}
					MeshRenderer renderer = meshNode.gameObject.GetComponent<MeshRenderer>();
					if (renderer == null) {
						renderer = (MeshRenderer)meshNode.gameObject.AddComponent(typeof(MeshRenderer));
					}
					meshFilter.mesh = xgMesh.meshes[ii];
					Material[] meshMaterials = new Material[1];
					meshMaterials[0] = materials[xgMesh.materials[ii]];
					renderer.materials = meshMaterials;
				}
			}
			items[itemIndex] = item.gameObject;
		}
		
		for(int itemIndex=0; itemIndex < items.Length; itemIndex++) {
			int parentIndex = hierarchy[itemIndex*2+1];
			items[itemIndex].transform.parent = parentIndex < 0 ? gameObject.transform : items[parentIndex].transform;
		}
		
		AnimationClip[] animationClip = importAnimationClips(items, anim);
		saveAnimationAssets(animationClip, stageAnimationRoot, namePrefix);
		
		gameObject.transform.localEulerAngles = new Vector3(180,0,0);

		wipePrefabEmbeddedAssets(prefabPath);
		PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, prefabPath, InteractionMode.UserAction);

		RenderSettings.ambientLight = Color.white;
		
		saveSceneAsset(stageSceneRoot, namePrefix);
	}
	
	static void importStage(uint fileIndex) {
		string xenogearsRoot = createFolderIfNotExistent("Assets", "Xenogears");
		string stageRoot = createFolderIfNotExistent(xenogearsRoot, "Stage");
		string stageModelRoot = createFolderIfNotExistent(stageRoot, "Model");
		string stageSceneRoot = createFolderIfNotExistent(stageRoot, "Scene");
		string stageMeshRoot = createFolderIfNotExistent(stageRoot, "Mesh");
		string stageTextureRoot = createFolderIfNotExistent(stageRoot, "Texture");
		string stageMaterialRoot = createFolderIfNotExistent(stageRoot, "Material");

		var namePrefix = "stage" + fileIndex;

		uint diskIndex = 1; // there are disk 1 and disk 2
		uint dirIndex = 18; // 0-based index
			
		string dataPath = ToUnityPath(Path.Combine( Path.Combine(Application.dataPath, ".."), "Data"));
		string stagePath = ToUnityPath(Path.Combine( dataPath, Path.Combine( "disk" + diskIndex, "dir" + dirIndex)));
		string filePath = ToUnityPath(Path.Combine( stagePath, "file" + (fileIndex * 2) + ".bin"));

		EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
		
		byte[] data = File.ReadAllBytes(filePath);	

		string prefabPath = ToUnityPath(Path.Combine(stageModelRoot, namePrefix + ".prefab"));

		XGModel model = importStageModel(data);	
		saveMeshAssets(model, stageMeshRoot, namePrefix);

		XGTexture[] textures = importStageTextures(data, model.shaders);
		Texture2D[] texture2ds = saveTextureAssets(textures, stageTextureRoot, namePrefix);

		Material[] materials = new Material[model.shaders.Count];
		saveMaterialAssets(model, textures, texture2ds, materials, stageMaterialRoot, namePrefix);
		
		List<int> hierarchy = new List<int>();
		uint offset = getUInt32LE(data, 12);
		short block = (short)getUInt16LE(data, offset);
		short parent = (short)getUInt16LE(data, offset + 2);
		offset += 4;
		while (block != -2) {
			hierarchy.Add(block);
			hierarchy.Add(parent);
			block = (short)getUInt16LE(data, offset);
			parent = (short)getUInt16LE(data, offset + 2);
			offset += 4;
		}
		
		GameObject[] items = new GameObject[hierarchy.Count / 2];
		
		GameObject gameObject = new GameObject(namePrefix);

		for(int itemIndex=0; itemIndex < items.Length; itemIndex++) {
			XGMesh xgMesh = model.meshes[hierarchy[itemIndex*2+0]];
			var itemName = namePrefix + "_item" + itemIndex;
			Transform item = gameObject.transform.Find(itemName);
			if (item == null) {
				item = new GameObject(itemName).transform;
			}
			for (int ii = 0; ii < xgMesh.meshes.Length; ii += 1) {
				var meshNodeName = namePrefix + "_item" + itemIndex + "_mesh" + ii;
				Transform meshNode = item.Find(meshNodeName);
				if (meshNode == null) {
					meshNode = new GameObject(meshNodeName).transform;
				}
				meshNode.parent = item;
				MeshFilter meshFilter = meshNode.gameObject.GetComponent<MeshFilter>();
				if (meshFilter == null) {
					meshFilter = (MeshFilter)meshNode.gameObject.AddComponent(typeof(MeshFilter));
				}
				MeshRenderer renderer = meshNode.gameObject.GetComponent<MeshRenderer>();
				if (renderer == null) {
					renderer = (MeshRenderer)meshNode.gameObject.AddComponent(typeof(MeshRenderer));
				}
				meshFilter.mesh = xgMesh.meshes[ii];
				Material[] meshMaterials = new Material[1];
				meshMaterials[0] = materials[xgMesh.materials[ii]];
				renderer.materials = meshMaterials;
			}
			
			items[itemIndex] = item.gameObject;
		}
		
		for(int itemIndex=0; itemIndex < items.Length; itemIndex++) {
			int parentIndex = hierarchy[itemIndex*2+1];
			items[itemIndex].transform.parent = parentIndex < 0 ? gameObject.transform : items[parentIndex].transform;
		}
		
		gameObject.transform.localEulerAngles = new Vector3(180,0,0);

		wipePrefabEmbeddedAssets(prefabPath);
		PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, prefabPath, InteractionMode.UserAction);

		RenderSettings.ambientLight = Color.white;
		
		saveSceneAsset(stageSceneRoot, namePrefix);
	}

	static void importTerrain(uint fileIndex) {
		string xenogearsRoot = createFolderIfNotExistent("Assets", "Xenogears");
		string stageRoot = createFolderIfNotExistent(xenogearsRoot, "Worldmap");
		string stageModelRoot = createFolderIfNotExistent(stageRoot, "Model");
		string stageSceneRoot = createFolderIfNotExistent(stageRoot, "Scene");
		string stageTerrainRoot = createFolderIfNotExistent(stageRoot, "Terrain");
		string stageTerrainLayerRoot = createFolderIfNotExistent(stageRoot, "TerrainLayer");
		string stageTextureRoot = createFolderIfNotExistent(stageRoot, "Texture");

		string namePrefix = "worldmap" + fileIndex;

		uint diskIndex = 1; // there are disk 1 and disk 2
		uint dirIndex = 26 + fileIndex; // 0-based index
			
		string dataPath = ToUnityPath(Path.Combine( Path.Combine(Application.dataPath, ".."), "Data"));
		string terrainPath = ToUnityPath(Path.Combine( dataPath, Path.Combine( "disk" + diskIndex, "dir" + dirIndex)));
		string filePath = ToUnityPath(Path.Combine( terrainPath, "file" + 8 + ".bin"));
		string texturePath = ToUnityPath(Path.Combine( terrainPath, "file" + 1 + ".bin"));

		EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

		string prefabPath = ToUnityPath(Path.Combine(stageModelRoot, namePrefix + ".prefab"));
		
		byte[] data = File.ReadAllBytes(filePath);
		byte[] textureData = loadLzs(texturePath);
		TerrainLayer[] terrainLayers = new TerrainLayer[1];
		
		uint[] image = new uint[4096*4096];
		for(uint ytex=0;ytex<4; ytex++) {
			for(uint xtex=0; xtex<4; xtex++) {
				for (uint i=0; i<4; i++) {
					for (uint j=0; j<4; j++) {
						uint terrainOffset = ((i+ytex*4) * 16 + (j+xtex*4)) * 2048;
						for (int y=0; y<16; y++) {
							for (int x=0; x<16; x++) {
								// Debug.Log ("i:"+i+" j:"+j+" y:"+y+" x:"+x);
								int xt = x / 8;
								int yt = y / 8;
								int xp = x % 8;
								int yp = y % 8;
								uint adr = (uint)(terrainOffset + ((yt * 2 + xt) * 9 * 9 + yp * 9 + xp) * 4);
								
								byte attr = data[adr + 1];
								bool water = (attr & 0x10) != 0;
								bool flipU = (attr & 0x20) != 0;
								bool flipV = (attr & 0x40) != 0;
								uint textureIdx = (uint)(attr & 0x7);
							
								byte uv = data[adr + 2];
								int v = (uv >> 4) * 16;
								int u = (uv & 0xF) * 16;
								uint textureOffset = getUInt32LE(textureData, 4 + textureIdx * 4);
								for (int yy=0; yy<16; yy++) {
									for (int xx=0; xx<16; xx++) {
										int yf = flipV ? 15-yy : yy;
										int xf = flipU ? 15-xx : xx;
										int index = textureData[textureOffset + 0x220 + (v+yf) * 256 + (u+xf)];
										ushort col = getUInt16LE(textureData, (uint)(textureOffset + 0x14 + index * 2));
									    int r = (((col     ) & 31) * 255 + 15) / 31;
									    int g = (((col >>  5) & 31) * 255 + 15) / 31;
									    int b = (((col >> 10) & 31) * 255 + 15) / 31;
										image[((xtex * (4096 * 1024)) + (((j*16+x)*16+xx) * 4096)) + (ytex * 1024) + ((i*16+y)*16+yy)] = (((uint)r & 0xFF) << 0) | (((uint)g & 0xFF) << 8) | (((uint)b & 0xFF) << 16) | (((uint)0xFF) << 24);
									}
								}
							}
						}
					}
				}
			}
		}
		string imageFilePath = ToUnityPath(Path.Combine(stageTextureRoot, namePrefix + "_texture0.png"));
		WritePNG(image, 4096, 4096, imageFilePath);
		AssetDatabase.ImportAsset(imageFilePath);

		Texture2D texture2d = (Texture2D)AssetDatabase.LoadMainAssetAtPath(imageFilePath);

		TerrainLayer terrainLayer = new TerrainLayer();
		terrainLayer.diffuseTexture = texture2d;
		terrainLayer.tileOffset = new Vector2(0, 0);
		terrainLayer.tileSize = new Vector2(256, 256);
		AssetDatabase.CreateAsset(terrainLayer, ToUnityPath(Path.Combine(stageTerrainLayerRoot, namePrefix + "_terrainlayer0.terrainlayer")));
		terrainLayers[0] = terrainLayer;

		float[,] heights = new float[256,256];
		for (uint i=0; i<16; i++) {
			for (uint j=0; j<16; j++) {
				uint terrainOffset = (i * 16 + j) * 2048;
				for (int y=0; y<16; y++) {
					for (int x=0; x<16; x++) {
						int xt = x / 8;
						int yt = y / 8;
						int xp = x % 8;
						int yp = y % 8;
						uint adr = (uint)(terrainOffset + ((yt * 2 + xt) * 9 * 9 + yp * 9 + xp) * 4);
						
						byte attr = data[adr + 1];
						bool water = (attr & 0x10) != 0; // ?
						bool flipU = (attr & 0x20) != 0;
						bool flipV = (attr & 0x40) != 0;
						
						byte uv = data[adr + 2];
						int v = (uv >> 4) * 16;
						int u = (uv & 0xF) * 16;
						int h = -((sbyte)data[adr + 0]);
						
						heights[j*16+x, i*16+y] = ((float)h) / 128.0f;
					}
				}
			}
		}
		
		TerrainData terrainData = new TerrainData();
		terrainData.name = "terrain";
		terrainData.heightmapResolution = 256;
		terrainData.size = new Vector3(256,16,256);
		terrainData.SetHeights(0, 0, heights);
		terrainData.terrainLayers = terrainLayers;
		
		AssetDatabase.CreateAsset(terrainData, ToUnityPath(Path.Combine(stageTerrainRoot, namePrefix + "_terrain0.asset")));

		GameObject gameObject = new GameObject(namePrefix);

		Terrain terrain = gameObject.GetComponent<Terrain>();
		if (terrain == null) {
			terrain = (Terrain)gameObject.AddComponent(typeof(Terrain));
		}
		terrain.terrainData = terrainData;
		terrain.materialTemplate = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Terrain-Standard.mat");
		TerrainCollider terrainCollider = gameObject.GetComponent<TerrainCollider>();
		if (terrainCollider == null) {
			terrainCollider = (TerrainCollider)gameObject.AddComponent(typeof(TerrainCollider));
		}
		terrainCollider.terrainData = terrainData;

		wipePrefabEmbeddedAssets(prefabPath);
		PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, prefabPath, InteractionMode.UserAction);

		// Need to save assets or the weights don't work
		AssetDatabase.SaveAssets();

		terrain.Flush();

		RenderSettings.ambientLight = Color.white;
		
		saveSceneAsset(stageSceneRoot, namePrefix);
	}
	
	static byte[] readSectorForm1(FileStream fileStream, int lba, int count) {
		byte[] data = new byte[2048 * count];
		for(int i=0; i<count; i++) {
			fileStream.Seek((lba + i) * 2352 + 24, SeekOrigin.Begin);
			fileStream.Read(data, i*2048, 2048);
		}
		return data;
	}
	
	static void readDir(List<FileEntry> fileEntry, FileStream fileStream, string path, int dir_pos, int dir_size, int parent) {
		System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
		byte[] dir = readSectorForm1(fileStream, dir_pos, (dir_size + 2047) / 2048);
		int pos = 0;
		while (pos < dir.Length) {
			int entry_size = dir[pos + 0];
			int file_pos = (int)getUInt32LE(dir, (uint)(pos + 2));
			int file_len = (int)getUInt32LE(dir, (uint)(pos + 10));
			int attr = dir[pos + 25];
			int name_len = dir[pos + 32];
			if (entry_size > 0) {
			    bool hidden = (attr & 1) != 0;
			    bool subdir = (attr & 2) != 0;
			
			    if (file_pos != dir_pos && file_pos != parent) {
					string name = enc.GetString(dir, pos + 33, name_len).Trim();
					int semicolon = name.IndexOf(';');
					if (semicolon >= 0) {
						name = name.Substring(0, semicolon);
					}
				
					string file_path = Path.Combine(path, name);
					// Debug.Log("name:"+file_path +" file_pos:" + file_pos + " file_len:" + file_len);
					if (subdir) {
						readDir(fileEntry, fileStream, file_path, file_pos, file_len, dir_pos);
					} else {
						FileEntry entry = new FileEntry();
						entry.name = file_path;
						entry.pos = file_pos;
						entry.size = file_len;
						fileEntry.Add(entry);
					}
				}
			    pos += entry_size;
			} else {
			    pos = (pos + 2047) & ~2047;
			}
		}
	}
	
	static void readFileTable(List<FileEntry> fileEntry, FileStream fileStream) {
		byte[] fileTable = readSectorForm1( fileStream, 24, 16 );
		int index = 0;
		int fileCount = 0;
		int dirCount = 0;
		int dirIndex = 0;
		int numFiles = 0;
		List<int> stackList = new List<int>();
		while (true) {
			int startSector = (int)(getUInt32LE( fileTable, (uint)(index * 7 + 0)) & 0xFFFFFF);
			if (startSector == 0xFFFFFF) {
			    return;
			}
			int fileSize = (int)getUInt32LE( fileTable, (uint)(index * 7 + 3));
			if (fileSize < 0) {
				// Debug.Log("numFiles:"+numFiles+" fileSize:"+fileSize);
				stackList.Add (fileCount);
				numFiles = -fileSize;
			    fileCount = 0;
			    dirIndex = dirCount;
			    dirCount ++;
			} else if (fileSize > 0) {
			    string file_path = "file" + fileCount + ".bin";
				if (numFiles > 0) {
					file_path = Path.Combine("dir" + dirIndex, file_path);
					numFiles --;
					if(numFiles == 0) {
						fileCount = stackList[stackList.Count-1];
						stackList.RemoveAt(stackList.Count-1);
					}
				}
				FileEntry entry = new FileEntry();
				entry.name = file_path;
				entry.pos = startSector;
				entry.size = fileSize;
				fileEntry.Add(entry);

			    fileCount ++;
			}
			index ++;
		}
	}
	
	static void importDisc(int disc, string filename) {
		FileStream fileStream = File.Open(filename, FileMode.Open);

		byte[] volume_descriptor = readSectorForm1( fileStream, 16, 1 );
		
		System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
		string system_identifier = enc.GetString(volume_descriptor, 8, 32).Trim();
		string volume_identifier = enc.GetString(volume_descriptor, 40, 32).Trim();
		
		// read the iso9660 filesystem 
		int root_pos = (int)getUInt32LE( volume_descriptor, 156 + 2);
		int root_len = (int)getUInt32LE( volume_descriptor, 156 + 10);
		List<FileEntry> isoFileEntry = new List<FileEntry>();
		readDir(isoFileEntry, fileStream, "", root_pos, root_len, root_pos);
		
		// read the hidden filesystem
		List<FileEntry> hiddenFileEntry = new List<FileEntry>();
		readFileTable(hiddenFileEntry, fileStream);
		
		// check all names if they refer to files in the iso9660 filesystem
		foreach(FileEntry entry in hiddenFileEntry) {
			foreach(FileEntry entry2 in isoFileEntry) {
				if(entry.pos == entry2.pos) {
					entry.name = entry2.name;
				}
			}		
		}		
		
		string dataPath = ToUnityPath(Path.Combine( Path.Combine(Application.dataPath, ".."), "Data"));
		string diskPath = ToUnityPath(Path.Combine( dataPath, "disk" + disc ));
		string csvPath =  ToUnityPath(Path.Combine( dataPath, "disk" + disc + ".csv" ));
		
		for(int i=0; i<hiddenFileEntry.Count; i++) {
			FileEntry entry = hiddenFileEntry[i];

			string filePath = ToUnityPath(Path.Combine(diskPath, entry.name));
			string directory = Path.GetDirectoryName(filePath);
			Directory.CreateDirectory(directory);
			
			// check if we're looking at a data or movie sector grab the format field from the sub header
			fileStream.Seek(entry.pos * 2352 + 19, SeekOrigin.Begin);
			int format = fileStream.ReadByte();
			// Debug.Log("filePath:"+filePath+" "+format);
			if (format == 0) {
				// data
				byte[] data = readSectorForm1(fileStream, entry.pos, (entry.size + 2047) / 2048);
				byte[] file = new byte[entry.size];
				Array.Copy(data, file, entry.size);
				File.WriteAllBytes(filePath, file);
			} else {
				// movie (raw sectors)
				fileStream.Seek(entry.pos * 2352, SeekOrigin.Begin);
				int block = (entry.size + 2335) / 2336;
				byte[] file = new byte[block * 2352];
				fileStream.Read(file, 0, block * 2352);
				File.WriteAllBytes(filePath, file);
			}
		}
		
		fileStream.Close();

		StreamWriter csvStream = new StreamWriter(csvPath);
		foreach(FileEntry entry in hiddenFileEntry) {
	        csvStream.Write(entry.name + "," + entry.pos + "," + entry.size + "\n" );
		}
		csvStream.Close ();
	}
	
	static void importTim(int diskIndex, int dirIndex, int fileIndex, string name) {
		string xenogearsRoot = createFolderIfNotExistent("Assets", "Xenogears");
		string imageRoot = createFolderIfNotExistent(xenogearsRoot, "Images");
		string imageGroupRoot = createFolderIfNotExistent(imageRoot, name);		
		
		string dataPath = ToUnityPath(Path.Combine( Path.Combine(Application.dataPath, ".."), "Data"));
		string terrainPath = ToUnityPath(Path.Combine( dataPath, Path.Combine( "disk" + diskIndex, "dir" + dirIndex)));
		string filePath = ToUnityPath(Path.Combine( terrainPath, "file" + fileIndex + ".bin"));

		byte[] data = File.ReadAllBytes(filePath);

		uint format = getUInt32LE(data, 4);
		uint bpp = format & 3;
		bool clp = (format & 8) != 0;
		
		byte[] vram = new byte[2048 * 1024];
		
		// unpack clut
		uint ofs = 8;
		uint clut_ymin = 512;
		uint clut_ymax = 0;
		if (clp) {
			uint clut_size = getUInt32LE (data, ofs);
			uint clut_end = ofs + clut_size;
			ofs += 4;
			while (ofs < clut_end) {
				ushort clut_x = getUInt16LE(data, ofs + 0);
				ushort clut_y = getUInt16LE(data, ofs + 2);
				ushort clut_width = getUInt16LE(data, ofs + 4);
				ushort clut_height = getUInt16LE(data, ofs + 6);
				ofs += 8;
				
				clut_ymin = (uint)Math.Min(clut_ymin, clut_y);
				clut_ymax = (uint)Math.Max(clut_ymax, clut_y + clut_height);
				
				for (int j=0; j < clut_height; j++) {
					uint vramPos = (uint)((clut_y + j) * 2048 + clut_x * 2);
					for(int k=0; k<clut_width*2; k++) {
						vram[vramPos++] = data[ofs++]; 
					}
				}
			}
		}
		
		// unpack images
		uint image_ymin = 512;
		uint image_ymax = 0;
		uint image_xmin = 2048;
		uint image_xmax = 0;
		uint image_size = getUInt32LE (data, ofs);
		uint image_end = ofs + image_size;
		ofs += 4;
		while (ofs < image_end) {
			ushort image_x = getUInt16LE(data, ofs + 0);
			ushort image_y = getUInt16LE(data, ofs + 2);
			ushort image_width = getUInt16LE(data, ofs + 4);
			ushort image_height = getUInt16LE(data, ofs + 6);
			ofs += 8;
			
			image_ymin = (uint)Math.Min(image_ymin, image_y);
			image_ymax = (uint)Math.Max(image_ymax, image_y + image_height);

			image_xmin = (uint)Math.Min(image_xmin, image_x);
			image_xmax = (uint)Math.Max(image_xmax, image_x + image_width);
			
			for (int j=0; j < image_height; j++) {
				uint vramPos = (uint)((image_y + j) * 2048 + image_x * 2);
				bool contains_nonzero = false;
				for(int k=0; k<image_width*2; k++) {
					if (data[ofs + k] != 0) {
						contains_nonzero = true;
						break;
					}
				}
				if (contains_nonzero == false) {
					continue;
				}
				for(int k=0; k<image_width*2; k++) {
					vram[vramPos++] = data[ofs++]; 
				}
			}
		}
		
		int width = (int)(image_xmax - image_xmin);
		int height = (int)(image_ymax - image_ymin);
		
		switch(bpp) {
		case 0:
			width *= 4;
			break;
		case 1:
			width *= 2;
			break;
		}
		
		uint[] image = new uint[width*height];
		switch(bpp) {
			case 0: { // 4-bit
				uint[] pal = new uint[16];
				for (int idx=0; idx<16; idx++) {
					uint vaddr = (uint)(clut_ymin * 2048 + idx * 2);
					ushort col = (ushort)(vram[vaddr] + vram[vaddr+1] * 256);
					pal[idx] = getColour(col, false, 255);
				}
				for (int y=0; y<height; y++) {
					for (int x=0; x<width; x++) {
		    			int val = vram[(image_ymax - 1 - y) * 2048 + (x/2) + image_xmin * 2];
						int idx = (x & 1) != 0 ? val >> 4 : val & 0xF;
						image[y * width + x] = pal[idx];
					}
				}
				break;
			}
		    case 1: {
				uint[] pal = new uint[256];
				for (int idx=0; idx<256; idx++) {
					uint vaddr = (uint)(clut_ymin * 2048 + idx * 2);
					ushort col = (ushort)(vram[vaddr] + vram[vaddr+1] * 256);
					pal[idx] = getColour(col, false, 255);
				}
				for (int y=0; y<height; y++) {
					for (int x=0; x<width; x++) {
						uint idx = vram[(image_ymax - 1 - y) * 2048 + x + image_xmin * 2];
						image[y * width + x] = pal[idx];
					}
				}
				break;
			}
		    case 2: {
				for (int y=0; y<height; y++) {
					for (int x=0; x<width; x++) {
					    uint vaddr = (uint)((image_ymax - 1 - y) * 2048 + x * 2 + image_xmin * 2);
					    ushort col = (ushort)(vram[vaddr] + vram[vaddr+1] * 256);
						image[y * width + x]  = getColour(col, false, 255);
					}
				}
				break;
			}
		}

		string imageFilePath = ToUnityPath(Path.Combine(imageGroupRoot, "texture" + fileIndex + ".png"));
		WritePNG(image, width, height, imageFilePath);
		AssetDatabase.ImportAsset(imageFilePath);
	}
	
	void OnEnable() {
		disc1 = EditorPrefs.GetString("XGDisc1", disc1);
		disc2 = EditorPrefs.GetString("XGDisc2", disc2);

		exportDiscs = EditorPrefs.GetInt("XGDiscsVersion", 0) < discsVersion;
		exportField = EditorPrefs.GetInt("XGFieldVersion", 0) < fieldVersion;
		exportStage = EditorPrefs.GetInt("XGExportStage", 0) < stageVersion;
		exportTerrain = EditorPrefs.GetInt("XGExportTerrain", 0) < terrainVersion;
		exportHeads = EditorPrefs.GetInt("XGExportHeads", 0) < headsVersion;
		exportSlides = EditorPrefs.GetInt("XGExportSlides", 0) < slidesVersion;
		exportSceneModel = EditorPrefs.GetInt("XGExportSceneModel", 0) < sceneVersion;
	}
	
    void OnGUI () {
		EditorGUI.BeginDisabledGroup (doExport);
		exportDiscs = EditorGUILayout.BeginToggleGroup ("Unpack Discs", exportDiscs);
		disc1 = EditorGUILayout.TextField ("Disc 1", disc1);
		disc2 = EditorGUILayout.TextField ("Disc 2", disc2);
		if (GUILayout.Button("Choose Disc Image")) {
			filename = EditorUtility.OpenFilePanel("Choose Disc Image", filename, "img");
			if( filename.Length > 0 ) {
				// check header for correct block size and game
				FileStream fileStream = File.Open(filename, FileMode.Open);

				// identify the disk
				byte[] volume_descriptor = readSectorForm1( fileStream, 16, 1 );
				
				System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
				string system_identifier = enc.GetString(volume_descriptor, 8, 32).Trim();
				string volume_identifier = enc.GetString(volume_descriptor, 40, 32).Trim();
				
				// Debug.Log ("a:"+system_identifier+" b:"+volume_identifier);
				bool systemValid = system_identifier == "PLAYSTATION";
				bool volumeValid = volume_identifier == "XENOGEARS";
				
				int discId = -1;
				if (systemValid && volumeValid) {
					// read the filesystem to identify the disc
					int root_pos = (int)getUInt32LE( volume_descriptor, 156 + 2);
					int root_len = (int)getUInt32LE( volume_descriptor, 156 + 10);
					List<FileEntry> fileEntry = new List<FileEntry>();
					readDir(fileEntry, fileStream, "", root_pos, root_len, root_pos);
					foreach(FileEntry entry in fileEntry) {
						if (entry.name.Equals("SLUS_006.64")) {
							discId = 1;
						} else if (entry.name.Equals("SLUS_006.69")) {
							discId = 2;
						}
					}
				}
				fileStream.Close();
				
				// if the file format is not correct, bring up a dialog but allow to use it anyway.
				if (discId < 0) {
					if( EditorUtility.DisplayDialog("Error", "This doesn't seem to be a Xenogears disc image. Use it anyway?", "Yes", "No")) {
						disc1 = filename;
						disc2 = filename;
					}
				} else {
					switch(discId) {
					case 1:
						disc1 = filename;
						break;
					case 2:
						disc2 = filename;
						break;
					}
				}
			}
		}
        EditorGUILayout.EndToggleGroup ();
		
        exportField = EditorGUILayout.Toggle ("Import Fields", exportField);
        exportFieldIndices = EditorGUILayout.TextField ("Field Indices", exportFieldIndices);
        attachXGFieldNode = EditorGUILayout.Toggle ("Attach XGFieldNode", attachXGFieldNode);
		exportStage = EditorGUILayout.Toggle ("Import Stages", exportStage);
		exportStageIndices = EditorGUILayout.TextField ("Stage Indices", exportStageIndices);
		exportTerrain = EditorGUILayout.Toggle ("Import Terrain", exportTerrain);
		exportTerrainIndices = EditorGUILayout.TextField ("Terrain Indices", exportTerrainIndices);
		exportHeads = EditorGUILayout.Toggle ("Import Heads", exportHeads);
		exportHeadIndices = EditorGUILayout.TextField ("Head Indices", exportHeadIndices);
		exportSlides = EditorGUILayout.Toggle ("Import Slides", exportSlides);
		exportSlideIndices = EditorGUILayout.TextField ("Slide Indices", exportSlideIndices);
		exportSceneModel = EditorGUILayout.Toggle ("Import Scene Models", exportSceneModel);
		exportSceneModelIndices = EditorGUILayout.TextField ("Scene Model Indices", exportSceneModelIndices);
		
		if (GUILayout.Button("Import")) {
			doExport = true;
		}
		
		debugFeatures = EditorGUILayout.Foldout(debugFeatures, "Show Debug Features");
		if (debugFeatures) {
			if(GUILayout.Button ("Build package")) {
				string[] files = new string[2];
				files[0] = "Assets/Editor";
				files[1] = "Assets/Scripts";
				AssetDatabase.ExportPackage(files, "../ImportXenogears.unitypackage", ExportPackageOptions.Recurse);
			}
		}
		
		EditorGUI.EndDisabledGroup ();
	}

	void Update() {
		if (doExport) {
			string saveCurrentScene = EditorSceneManager.GetActiveScene().path;
			try {
				if (exportDiscs) {
					importDisc(1, disc1);
					importDisc(2, disc2);
					exportDiscs = false;
					EditorPrefs.SetInt("XGDiscsVersion", discsVersion);
					EditorPrefs.SetString("XGDisc1", disc1);
					EditorPrefs.SetString("XGDisc2", disc2);
				}
				if (exportField) {
					List<uint> indicesList = new List<uint>();
					splitIndicesString(exportFieldIndices, indicesList);
					for(uint i=0; i<730; i++) {
						if (indicesList.Count != 0 && !indicesList.Contains(i)) {
							continue;
						}
						importField (i, attachXGFieldNode);
					}
					exportField = false;
					EditorPrefs.SetInt("XGFieldVersion", fieldVersion);
					
					// importField (95, attachXGFieldNode);
				}
				if (exportStage) {
					List<uint> indicesList = new List<uint>();
					splitIndicesString(exportStageIndices, indicesList);
					for(uint i=0; i<75; i++) {
						if (indicesList.Count != 0 && !indicesList.Contains(i)) {
							continue;
						}
						importStage (i);
					}
					exportStage = false;
					EditorPrefs.SetInt("XGExportStage", stageVersion);
	//					importStage (20);
				}
				if (exportTerrain) {
					List<uint> indicesList = new List<uint>();
					splitIndicesString(exportTerrainIndices, indicesList);
					for(uint i=0; i<17; i++) {
						if (indicesList.Count != 0 && !indicesList.Contains(i)) {
							continue;
						}
						importTerrain (i);
					}
					exportTerrain = false;
					EditorPrefs.SetInt("XGExportTerrain", terrainVersion);
				}
				if (exportHeads) {
					List<uint> indicesList = new List<uint>();
					splitIndicesString(exportHeadIndices, indicesList);
					for(uint i=0; i<91; i++) {
						if (indicesList.Count != 0 && !indicesList.Contains(i)) {
							continue;
						}
						importTim(1, 9, (int)i, "Heads");
					}
					exportHeads = false;
					EditorPrefs.SetInt("XGExportHeads", headsVersion);
				}
				if (exportSlides) {
					List<uint> indicesList = new List<uint>();
					splitIndicesString(exportSlideIndices, indicesList);
					for(uint i=0; i<88; i++) {
						if (indicesList.Count != 0 && !indicesList.Contains(i)) {
							continue;
						}
						importTim(1, 14, (int)i, "Slides");
					}
					exportSlides = false;
					EditorPrefs.SetInt("XGExportSlides", slidesVersion);
				}
				if (exportSceneModel) {
					List<uint> indicesList = new List<uint>();
					splitIndicesString(exportSceneModelIndices, indicesList);
					for(uint i=0; i<72; i++) {
						if (indicesList.Count != 0 && !indicesList.Contains(i)) {
							continue;
						}
						importSceneModel(i);
					}
					exportSceneModel = false;
					EditorPrefs.SetInt("XGExportSceneModel", sceneVersion);
	//				importSceneModel(0);
				}
			} 
			finally  {
				doExport = false;
			}
			AssetDatabase.SaveAssets();
			if (EditorSceneManager.GetActiveScene().path != saveCurrentScene) {
				EditorSceneManager.OpenScene(saveCurrentScene);
			}
		}
	}
}

