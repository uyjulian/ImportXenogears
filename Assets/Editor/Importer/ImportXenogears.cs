// C# xenogears importer:
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;

public class FileEntry {
	public string name;
	public int pos;
	public int size;
};

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
};

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
};

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
};

public class XGMesh {
	public Mesh mesh;
	public int[] materials;
};

public class XGModel {
	public List<XGMesh> meshes;
	public List<XGShader> shaders;
};

public class ImportXenogears : EditorWindow {
	string filename = "";
	string disc1 = "Xenogears1.img";
	string disc2 = "Xenogears2.img";
	bool exportDiscs = true;
	bool exportField = true;
	bool exportStage = true;
	bool exportTerrain = true;
	bool exportHeads = true;
	bool exportSlides = true;
	bool exportSceneModel = true;
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
			xgMesh.mesh = new Mesh();
			xgMesh.mesh.name = "part" + partIndex;
			xgMesh.mesh.vertices = vertices;
			xgMesh.mesh.normals = normals;
			xgMesh.mesh.uv = uv;
			xgMesh.mesh.colors = colors;
			xgMesh.mesh.subMeshCount = groupIndex;
			xgMesh.materials = new int[groupIndex];
			
			triangleStart = 0;
			for (int i = 0; i < groupIndex; i++) {
				xgMesh.materials[i] = materials[i];
				int[] submesh = new int[groups[i] * 3];
				for(int j=0; j<groups[i] * 3; j++) {
					submesh[j] = triangles[triangleStart * 3 + j];	
				}
				xgMesh.mesh.SetTriangles(submesh, i);	
				triangleStart += groups[i];
			}
			
			xgModel.meshes.Add(xgMesh);
		}
		return xgModel;
	}
	
	static Color getColour(ushort col, bool abe, int alpha) {
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
		return new Color((float)r/255.0f,(float)g/255.0f,(float)b/255.0f,(float)a/255.0f);
	}

	
	static Texture2D[] importFieldTextures(byte[] textureData, List<XGShader> shaderList) {
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
	    Texture2D[] textures = new Texture2D[shaderList.Count];
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
				
	    		Color[] image = new Color[256*256];
				switch(tp) {
					case 0: { // 4-bit
						Color[] pal = new Color[16];
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
						Color[] pal = new Color[256];
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
			
				Texture2D texture = new Texture2D(256, 256);
				texture.name = "texture" + shaderIndex;
				texture.SetPixels(image);
				texture.Apply();
				textures[shaderIndex] = texture;
			} else {
				textures[shaderIndex] = null;
			}
		}
			
	    return textures;
	}

	static void importField(uint fileIndex) {
		// Create Field Root
		string fieldGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine("Assets", "Field")));
		//Debug.Log("fieldGuid:"+fieldGuid);
		if (fieldGuid.Length == 0) {
			fieldGuid = AssetDatabase.CreateFolder("Assets", "Field");
		} 
		string fieldRoot = AssetDatabase.GUIDToAssetPath(fieldGuid);
		//Debug.Log("fieldRoot:"+fieldRoot);
		
		// Create Model Root
		string fieldModelGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine(fieldRoot, "Model")));
		if (fieldModelGuid.Length == 0) {
			fieldModelGuid = AssetDatabase.CreateFolder(fieldRoot, "Model");
		} 
		string fieldModelRoot = AssetDatabase.GUIDToAssetPath(fieldModelGuid);
		
		// Create Scene Root
		string fieldSceneGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine(fieldRoot, "Scene")));
		if (fieldSceneGuid.Length == 0) {
			fieldSceneGuid = AssetDatabase.CreateFolder(fieldRoot, "Scene");
		} 
		string fieldSceneRoot = AssetDatabase.GUIDToAssetPath(fieldSceneGuid);

		// Create Scene
		EditorApplication.NewScene();
		
		uint diskIndex = 1; // there are disk 1 and disk 2
		uint dirIndex = 11; // 0-based index

		string dataPath = ToUnityPath(Path.Combine( Path.Combine(Application.dataPath, ".."), "Data"));
		string fieldPath = ToUnityPath(Path.Combine( dataPath, Path.Combine( "disk" + diskIndex, "dir" + dirIndex)));
		string archivePath = ToUnityPath(Path.Combine( fieldPath, "file" + (fileIndex * 2) + ".bin"));
		string texturePath = ToUnityPath(Path.Combine( fieldPath, "file" + (fileIndex * 2 + 1) + ".bin"));

		byte[] archiveData = File.ReadAllBytes(archivePath);
		byte[] modelData = getData(archiveData, 2);
		byte[] textureData = File.ReadAllBytes(texturePath);
		
		UnityEngine.Object prefab = PrefabUtility.CreateEmptyPrefab (ToUnityPath(Path.Combine(fieldModelRoot, "field" + fileIndex + ".prefab")));
		
		XGModel model = importFieldModel(modelData);
		foreach (XGMesh xgMesh in model.meshes) {
			AssetDatabase.AddObjectToAsset(xgMesh.mesh, prefab);
		}
		
		Texture2D[] textures = importFieldTextures(textureData, model.shaders);
		for(int i=0; i<textures.Length; i++) {
			if (textures[i] != null) {
				AssetDatabase.AddObjectToAsset(textures[i], prefab);
			}
		}
		
		Shader opaqueShader = Shader.Find ("Diffuse");
		Shader cutoutShader = Shader.Find ("Transparent/Cutout/Diffuse");
		Shader transparentShader = Shader.Find ("Transparent/Diffuse");
		Material[] materials = new Material[model.shaders.Count];
		for (int i=0; i<model.shaders.Count; i++) {
			if (model.shaders[i].abe) {
				materials[i] = new Material(transparentShader);
			} else {
				// should probably check the texture if the cutout shader is required...
				materials[i] = new Material(cutoutShader);
			}
			materials[i].name = "material" + i;
			if (textures[i] != null) {
				materials[i].mainTexture = textures[i];
			}
			AssetDatabase.AddObjectToAsset(materials[i], prefab);
		}
		
		GameObject gameObject = new GameObject("field");
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
			
			GameObject item = new GameObject("item" + itemIndex);
			item.transform.parent = gameObject.transform;
			item.transform.Translate(pos_x, pos_y, pos_z);
			item.transform.Rotate(rot_x * 90.0f / 1024.0f, rot_y * 90.0f / 1024.0f, rot_z * 90.0f / 1024.0f);
			XGFieldNode xgFieldNode = (XGFieldNode)item.AddComponent (typeof(XGFieldNode));
			xgFieldNode.flag = new bool[16];
			for(int i=0; i<16; i++) {
				xgFieldNode.flag[i] = (flags & (1<<i)) != 0;
			}
			xgFieldNode.index = index;
			if ((flags & ((1<<5)|(1<<6)/*|(1<<7)|(1<<8)*/)) == 0) {
				XGMesh xgMesh = model.meshes[index]; 
				MeshFilter meshFilter = (MeshFilter)item.AddComponent(typeof(MeshFilter));
				MeshRenderer renderer = (MeshRenderer)item.AddComponent(typeof(MeshRenderer));
				meshFilter.mesh = xgMesh.mesh;
				Material[] meshMaterials = new Material[xgMesh.materials.Length];
				for(int i=0; i<xgMesh.materials.Length; i++) {
					meshMaterials[i] = materials[xgMesh.materials[i]];
				}
				renderer.materials = meshMaterials;
			}
		}
		gameObject.transform.localEulerAngles = new Vector3(180,0,0);
		
		PrefabUtility.ReplacePrefab(gameObject, prefab, ReplacePrefabOptions.ConnectToPrefab);

		RenderSettings.ambientLight = Color.white;
		
		EditorApplication.SaveScene(ToUnityPath(Path.Combine(fieldSceneRoot, "field" + fileIndex + ".unity")));
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
			xgMesh.mesh = new Mesh();
			xgMesh.mesh.name = "part"+blockIndex;
			xgMesh.mesh.vertices = vertices;
			xgMesh.mesh.normals = normals;
			xgMesh.mesh.uv = uv;
			xgMesh.mesh.colors = colors;
			xgMesh.mesh.subMeshCount = groupIndex;
			xgMesh.materials = new int[groupIndex];
			
			triangleStart = 0;
			for (int i = 0; i < groupIndex; i++) {
				xgMesh.materials[i] = materials[i];
				int[] submesh = new int[groups[i] * 3];
				for(int j=0; j<groups[i] * 3; j++) {
					submesh[j] = triangles[triangleStart * 3 + j];	
				}
				xgMesh.mesh.SetTriangles(submesh, i);	
				triangleStart += groups[i];
			}
			
			xgModel.meshes.Add(xgMesh);
		}
		
		return xgModel;
	}
	
	static Texture2D[] importStageTextures(byte[] textureData, List<XGShader> shaderList) {
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
				for(int k=0; k<width*2; k++) {
					vram[vramPos++] = textureData[dataPos++]; 
				}
			}
		}
	
	    // convert textures with their palette
	    Texture2D[] textures = new Texture2D[shaderList.Count];
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
				
	    		Color[] image = new Color[256*256];
				switch(tp) {
					case 0: { // 4-bit
						Color[] pal = new Color[16];
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
						Color[] pal = new Color[256];
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
			
				Texture2D texture = new Texture2D(256, 256);
				texture.name = "texture" + shaderIndex;
				texture.SetPixels(image);
				texture.Apply();
				textures[shaderIndex] = texture;
			} else {
				textures[shaderIndex] = null;
			}
		}
			
	    return textures;
	}
	
	static AnimationClip[] importAnimationClips(GameObject[] items, byte[] anim)
	{
		AnimationClip[] animationClip = new AnimationClip[0];
		
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
			Quaternion rotationX = Quaternion.Euler((float)rotX * 360.0f / 4096.0f, 0, 0);
			Quaternion rotationY = Quaternion.Euler(0, (float)rotY * 360.0f / 4096.0f, 0);
			Quaternion rotationZ = Quaternion.Euler(0, 0, (float)rotZ * 360.0f / 4096.0f);
			items[i].transform.localRotation = rotationX * rotationY * rotationZ;
		}
		
/*
		// get animations
		for(uint i=0; i<count3; i++) {
			uint ofs4 = ofs2 + getUInt32LE (anim, ofs2 + 4 + i * 4);
			uint siz4 = getUInt32LE (anim, ofs2 + 8 + i * 4) - getUInt32LE (anim, ofs2 + 4 + i * 4);
			numBones = getUInt16LE(anim, ofs4);
			// Debug.Log ("size:"+siz4);
			// Debug.Log ("frames:"+ ((siz4 - 0x18) / 12) / numBones);
			ushort[] test = new ushort[9];
			string line = "line:";
			for(uint j=01; j<9; j++) {
				test[j] = getUInt16LE(anim, ofs4 + j * 2);
				line += " " + test[j];
			}
			Debug.Log (line);
			ushort flags = getUInt16LE(anim, ofs4 + 4);
			ushort rotCount = getUInt16LE(anim, ofs4 + 0xC);
			ushort transCount = getUInt16LE(anim, ofs4 + 0xE);
			Debug.Log ("flags:"+flags);
			Debug.Log ("rotCount:"+rotCount);
			Debug.Log ("transCount:"+transCount);
		}
*/
		
		return animationClip;
	}
	
	static void importSceneModel(uint fileIndex) {
		// Create Stage Root
		string stageGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine("Assets", "SceneModel")));
		if (stageGuid.Length == 0) {
			stageGuid = AssetDatabase.CreateFolder("Assets", "SceneModel");
		} 
		string stageRoot = AssetDatabase.GUIDToAssetPath(stageGuid);
		
		// Create Model Root
		string stageModelGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine(stageRoot, "Model")));
		if (stageModelGuid.Length == 0) {
			stageModelGuid = AssetDatabase.CreateFolder(stageRoot, "Model");
		} 
		string stageModelRoot = AssetDatabase.GUIDToAssetPath(stageModelGuid);
		
		// Create Scene Root
		string stageSceneGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine(stageRoot, "Scene")));
		if (stageSceneGuid.Length == 0) {
			stageSceneGuid = AssetDatabase.CreateFolder(stageRoot, "Scene");
		} 
		string stageSceneRoot = AssetDatabase.GUIDToAssetPath(stageSceneGuid);
		
		uint diskIndex = 1; // there are disk 1 and disk 2
		uint dirIndex = 12; // 0-based index
			
		string dataPath = ToUnityPath(Path.Combine( Path.Combine(Application.dataPath, ".."), "Data"));
		string stagePath = ToUnityPath(Path.Combine( dataPath, Path.Combine( "disk" + diskIndex, "dir" + dirIndex)));
		string filePath = ToUnityPath(Path.Combine( stagePath, "file" + (fileIndex * 2 + 2) + ".bin"));
		string animPath = ToUnityPath(Path.Combine( stagePath, "file" + (fileIndex * 2 + 1) + ".bin"));

		EditorApplication.NewScene();
		
		byte[] data = File.ReadAllBytes(filePath);
		byte[] anim = File.ReadAllBytes(animPath);

		UnityEngine.Object prefab = PrefabUtility.CreateEmptyPrefab (ToUnityPath(Path.Combine(stageModelRoot, "scenemodel" + fileIndex + ".prefab")));

		XGModel model = importStageModel(data);	
		foreach (XGMesh xgMesh in model.meshes) {
			AssetDatabase.AddObjectToAsset(xgMesh.mesh, prefab);
		}

		Texture2D[] textures = importStageTextures(data, model.shaders);
		for(int i=0; i<textures.Length; i++) {
			if (textures[i] != null) {
				AssetDatabase.AddObjectToAsset(textures[i], prefab);
			}
		}
		
		Shader opaqueShader = Shader.Find ("Diffuse");
		Shader cutoutShader = Shader.Find ("Transparent/Cutout/Diffuse");
		Shader transparentShader = Shader.Find ("Transparent/Diffuse");

		Material[] materials = new Material[model.shaders.Count];
		for (int i=0; i<model.shaders.Count; i++) {
			if (model.shaders[i].abe) {
				materials[i] = new Material(transparentShader);
			} else {
				// should probably check the texture if the cutout shader is required...
				materials[i] = new Material(opaqueShader);
			}
			materials[i].name = "material" + i;
			if (textures[i] != null) {
				materials[i].mainTexture = textures[i];
			}
			AssetDatabase.AddObjectToAsset(materials[i], prefab);
		}
		
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
		
		GameObject gameObject = new GameObject("scenemodel");
		for(int itemIndex=0; itemIndex < items.Length; itemIndex++) {
			GameObject item = new GameObject("item" + itemIndex);
			int blockIndex = hierarchy[itemIndex*2+0];
			if (blockIndex >= 0) {
				XGMesh xgMesh = model.meshes[blockIndex]; 
				MeshFilter meshFilter = (MeshFilter)item.AddComponent(typeof(MeshFilter));
				MeshRenderer renderer = (MeshRenderer)item.AddComponent(typeof(MeshRenderer));
				meshFilter.mesh = xgMesh.mesh;
				Material[] meshMaterials = new Material[xgMesh.materials.Length];
				for(int i=0; i<xgMesh.materials.Length; i++) {
					meshMaterials[i] = materials[xgMesh.materials[i]];
				}
				renderer.materials = meshMaterials;
			}
			items[itemIndex] = item;
		}
		
		for(int itemIndex=0; itemIndex < items.Length; itemIndex++) {
			int parentIndex = hierarchy[itemIndex*2+1];
			items[itemIndex].transform.parent = parentIndex < 0 ? gameObject.transform : items[parentIndex].transform;
		}
		
		AnimationClip[] animationClip = importAnimationClips(items, anim);
		for (int i=0; i<animationClip.Length; i++) {
			AssetDatabase.AddObjectToAsset(animationClip[i], prefab);
		}
		
		gameObject.transform.localEulerAngles = new Vector3(180,0,0);
		
		PrefabUtility.ReplacePrefab(gameObject, prefab, ReplacePrefabOptions.ConnectToPrefab);
		
		RenderSettings.ambientLight = Color.white;
		
		EditorApplication.SaveScene(ToUnityPath(Path.Combine(stageSceneRoot, "scenemodel" + fileIndex + ".unity")));
	}
	
	static void importStage(uint fileIndex) {
		// Create Stage Root
		string stageGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine("Assets", "Stage")));
		if (stageGuid.Length == 0) {
			stageGuid = AssetDatabase.CreateFolder("Assets", "Stage");
		} 
		string stageRoot = AssetDatabase.GUIDToAssetPath(stageGuid);
		
		// Create Model Root
		string stageModelGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine(stageRoot, "Model")));
		if (stageModelGuid.Length == 0) {
			stageModelGuid = AssetDatabase.CreateFolder(stageRoot, "Model");
		} 
		string stageModelRoot = AssetDatabase.GUIDToAssetPath(stageModelGuid);
		
		// Create Scene Root
		string stageSceneGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine(stageRoot, "Scene")));
		if (stageSceneGuid.Length == 0) {
			stageSceneGuid = AssetDatabase.CreateFolder(stageRoot, "Scene");
		} 
		string stageSceneRoot = AssetDatabase.GUIDToAssetPath(stageSceneGuid);
		
		uint diskIndex = 1; // there are disk 1 and disk 2
		uint dirIndex = 18; // 0-based index
			
		string dataPath = ToUnityPath(Path.Combine( Path.Combine(Application.dataPath, ".."), "Data"));
		string stagePath = ToUnityPath(Path.Combine( dataPath, Path.Combine( "disk" + diskIndex, "dir" + dirIndex)));
		string filePath = ToUnityPath(Path.Combine( stagePath, "file" + (fileIndex * 2) + ".bin"));

		EditorApplication.NewScene();
		
		byte[] data = File.ReadAllBytes(filePath);	

		UnityEngine.Object prefab = PrefabUtility.CreateEmptyPrefab (ToUnityPath(Path.Combine(stageModelRoot, "stage" + fileIndex + ".prefab")));

		XGModel model = importStageModel(data);	
		foreach (XGMesh xgMesh in model.meshes) {
			AssetDatabase.AddObjectToAsset(xgMesh.mesh, prefab);
		}

		Texture2D[] textures = importStageTextures(data, model.shaders);
		for(int i=0; i<textures.Length; i++) {
			if (textures[i] != null) {
				AssetDatabase.AddObjectToAsset(textures[i], prefab);
			}
		}
		
		Shader opaqueShader = Shader.Find ("Diffuse");
		Shader cutoutShader = Shader.Find ("Transparent/Cutout/Diffuse");
		Shader transparentShader = Shader.Find ("Transparent/Diffuse");

		Material[] materials = new Material[model.shaders.Count];
		for (int i=0; i<model.shaders.Count; i++) {
			if (model.shaders[i].abe) {
				materials[i] = new Material(transparentShader);
			} else {
				// should probably check the texture if the cutout shader is required...
				materials[i] = new Material(opaqueShader);
			}
			materials[i].name = "material" + i;
			if (textures[i] != null) {
				materials[i].mainTexture = textures[i];
			}
			AssetDatabase.AddObjectToAsset(materials[i], prefab);
		}
		
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
		
		GameObject gameObject = new GameObject("stage");
		for(int itemIndex=0; itemIndex < items.Length; itemIndex++) {
			XGMesh xgMesh = model.meshes[hierarchy[itemIndex*2+0]]; 
			GameObject item = new GameObject("item" + itemIndex);
			MeshFilter meshFilter = (MeshFilter)item.AddComponent(typeof(MeshFilter));
			MeshRenderer renderer = (MeshRenderer)item.AddComponent(typeof(MeshRenderer));
			meshFilter.mesh = xgMesh.mesh;
			Material[] meshMaterials = new Material[xgMesh.materials.Length];
			for(int i=0; i<xgMesh.materials.Length; i++) {
				meshMaterials[i] = materials[xgMesh.materials[i]];
			}
			renderer.materials = meshMaterials;
			
			items[itemIndex] = item;
		}
		
		for(int itemIndex=0; itemIndex < items.Length; itemIndex++) {
			int parentIndex = hierarchy[itemIndex*2+1];
			items[itemIndex].transform.parent = parentIndex < 0 ? gameObject.transform : items[parentIndex].transform;
		}
		
		gameObject.transform.localEulerAngles = new Vector3(180,0,0);
		
		PrefabUtility.ReplacePrefab(gameObject, prefab, ReplacePrefabOptions.ConnectToPrefab);
		
		RenderSettings.ambientLight = Color.white;
		
		EditorApplication.SaveScene(ToUnityPath(Path.Combine(stageSceneRoot, "stage" + fileIndex + ".unity")));
	}

	static void importTerrain(uint fileIndex) {
		// Create Terrain Root
		string terrainGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine("Assets", "Worldmap")));
		if (terrainGuid.Length == 0) {
			terrainGuid = AssetDatabase.CreateFolder("Assets", "Worldmap");
		} 
		string terrainRoot = AssetDatabase.GUIDToAssetPath(terrainGuid);
		
		// Create Model Root
		string terrainModelGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine(terrainRoot, "Model")));
		if (terrainModelGuid.Length == 0) {
			terrainModelGuid = AssetDatabase.CreateFolder(terrainRoot, "Model");
		} 
		string terrainModelRoot = AssetDatabase.GUIDToAssetPath(terrainModelGuid);
		
		// Create Scene Root
		string terrainSceneGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine(terrainRoot, "Scene")));
		if (terrainSceneGuid.Length == 0) {
			terrainSceneGuid = AssetDatabase.CreateFolder(terrainRoot, "Scene");
		} 
		string terrainSceneRoot = AssetDatabase.GUIDToAssetPath(terrainSceneGuid);
		
		uint diskIndex = 1; // there are disk 1 and disk 2
		uint dirIndex = 26 + fileIndex; // 0-based index
			
		string dataPath = ToUnityPath(Path.Combine( Path.Combine(Application.dataPath, ".."), "Data"));
		string terrainPath = ToUnityPath(Path.Combine( dataPath, Path.Combine( "disk" + diskIndex, "dir" + dirIndex)));
		string filePath = ToUnityPath(Path.Combine( terrainPath, "file" + 8 + ".bin"));
		string texturePath = ToUnityPath(Path.Combine( terrainPath, "file" + 1 + ".bin"));

		EditorApplication.NewScene();

		UnityEngine.Object prefab = PrefabUtility.CreateEmptyPrefab (ToUnityPath(Path.Combine(terrainModelRoot, "worldmap" + fileIndex + ".prefab")));
		
		byte[] data = File.ReadAllBytes(filePath);
		byte[] textureData = loadLzs(texturePath);
		SplatPrototype[] splatPrototypes = new SplatPrototype[16];
		
		for(uint ytex=0;ytex<4; ytex++) {
			for(uint xtex=0; xtex<4; xtex++) {
				Color[] image = new Color[1024*1024];
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
										float r = (float)((col    ) & 31) / 31.0f;
										float g = (float)((col >>  5) & 31) / 31.0f;
										float b = (float)((col >> 10) & 31) / 31.0f;
										image[((j*16+x)*16+xx) * 1024 + ((i*16+y)*16+yy)] = new Color(r, g, b, 1.0f);
									}
								}
							}
						}
					}
				}
				Texture2D texture = new Texture2D(1024, 1024);
				texture.name = "texture" + (ytex*4+xtex);
				texture.wrapMode = TextureWrapMode.Clamp;
				texture.SetPixels(image);
				texture.Apply();
				AssetDatabase.AddObjectToAsset(texture, prefab);
				
				SplatPrototype splatPrototype = new SplatPrototype();
				splatPrototype.texture = texture;
				splatPrototype.tileOffset = new Vector2(-ytex*64, -xtex*64); 
				splatPrototype.tileSize = new Vector2(64, 64);
				splatPrototypes[ytex*4+xtex] = splatPrototype;
			}
		}

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
		terrainData.alphamapResolution = 64;
		terrainData.splatPrototypes = splatPrototypes;
		
		AssetDatabase.AddObjectToAsset(terrainData, prefab);

		GameObject gameObject = new GameObject("worlmap");
		Terrain terrain = (Terrain)gameObject.AddComponent(typeof(Terrain));
		terrain.terrainData = terrainData;
		TerrainCollider terrainCollider = (TerrainCollider)gameObject.AddComponent(typeof(TerrainCollider));
		terrainCollider.terrainData = terrainData;

		PrefabUtility.ReplacePrefab(gameObject, prefab, ReplacePrefabOptions.ConnectToPrefab);
		
		// Need to save assets or the weights don't work
		AssetDatabase.SaveAssets();
		
		// Blend the textures
		float[, ,] splatmapData = new float [ terrainData.alphamapWidth, terrainData.alphamapHeight, terrainData.alphamapLayers];
		int dw = terrainData.alphamapWidth / 4;
		int dh = terrainData.alphamapHeight / 4;
		for (int i = 0; i < terrainData.alphamapWidth; i++) {
		    for (int j = 0; j < terrainData.alphamapHeight; j++) {
				int idx = (i/dw)+(j/dh)*4;
				for (int k = 0; k < terrainData.alphamapLayers; k++) {
		        	splatmapData[i, j, k] = k != idx ? 0.0f : 1.0f; 
				}
		    }
		}
		terrainData.SetAlphamaps(0, 0, splatmapData);

		RenderSettings.ambientLight = Color.white;
		
		EditorApplication.SaveScene(ToUnityPath(Path.Combine(terrainSceneRoot, "worldmap" + fileIndex + ".unity")));
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
		string imageGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine("Assets", "Images")));
		if (imageGuid.Length == 0) {
			imageGuid = AssetDatabase.CreateFolder("Assets", "Images");
		} 
		string imageRoot = AssetDatabase.GUIDToAssetPath(imageGuid);
		
		string imageGroupGuid = AssetDatabase.AssetPathToGUID (ToUnityPath(Path.Combine(imageRoot, name)));
		if (imageGroupGuid.Length == 0) {
			imageGroupGuid = AssetDatabase.CreateFolder(imageRoot, name);
		} 
		string imageGroupRoot = AssetDatabase.GUIDToAssetPath(imageGroupGuid);
		
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
		
		Color[] image = new Color[width*height];
		switch(bpp) {
			case 0: { // 4-bit
				Color[] pal = new Color[16];
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
				Color[] pal = new Color[256];
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
	
		Texture2D texture = new Texture2D(width, height);
		texture.SetPixels(image);
		texture.Apply();
		byte[] pngData = texture.EncodeToPNG();
		
		string imageFilePath = ToUnityPath(Path.Combine(imageGroupRoot, "texture" + fileIndex + ".png"));
		File.WriteAllBytes(imageFilePath, pngData);
		AssetDatabase.ImportAsset(imageFilePath);

		TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(imageFilePath);
		textureImporter.textureType = TextureImporterType.GUI;
		textureImporter.wrapMode = TextureWrapMode.Clamp;
		textureImporter.linearTexture = false;
		textureImporter.textureFormat = TextureImporterFormat.ARGB16;
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
		exportStage = EditorGUILayout.Toggle ("Import Stages", exportStage);
		exportTerrain = EditorGUILayout.Toggle ("Import Terrain", exportTerrain);
		exportHeads = EditorGUILayout.Toggle ("Import Heads", exportHeads);
		exportSlides = EditorGUILayout.Toggle ("Import Slides", exportSlides);
		exportSceneModel = EditorGUILayout.Toggle ("Import Scene Models", exportSceneModel);
		
		if (GUILayout.Button("Import")) {
			doExport = true;
		}
		
		debugFeatures = EditorGUILayout.Foldout(debugFeatures, "Show Debug Features");
		if (debugFeatures) {
			if(GUILayout.Button ("Build package")) {
				string [] files = new string[2];
				files[0] = "Assets/Editor";
				files[1] = "Assets/Scripts";
				AssetDatabase.ExportPackage(files, "../ImportXenogears.unitypackage", ExportPackageOptions.Recurse);
			}
		}
		
		EditorGUI.EndDisabledGroup ();
	}

	void Update() {
		if (doExport) {
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
					
					for(uint i=0; i<730; i++) {
						importField (i);
					}
					exportField = false;
					EditorPrefs.SetInt("XGFieldVersion", fieldVersion);
					
					// importField (95);
				}
				if (exportStage) {
					for(uint i=0; i<75; i++) {
						importStage (i);
					}
					exportStage = false;
					EditorPrefs.SetInt("XGExportStage", stageVersion);
	//					importStage (20);
				}
				if (exportTerrain) {
					for(uint i=0; i<17; i++) {
						importTerrain (i);
					}
					exportTerrain = false;
					EditorPrefs.SetInt("XGExportTerrain", terrainVersion);
				}
				if (exportHeads) {
					for(int i=0; i<91; i++) {
						importTim(1, 9, i, "Heads");
					}
					exportHeads = false;
					EditorPrefs.SetInt("XGExportHeads", headsVersion);
				}
				if (exportSlides) {
					for(int i=0; i<88; i++) {
						importTim(1, 14, i, "Slides");
					}
					exportSlides = false;
					EditorPrefs.SetInt("XGExportSlides", slidesVersion);
				}
				if (exportSceneModel) {
					for(uint i=0; i<72; i++) {
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
		}
	}
}

