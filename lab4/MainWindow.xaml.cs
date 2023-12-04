﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Drawing;
using System.Timers;
using System.Numerics;
using System.Drawing.Imaging;
using System.Collections.Concurrent;

namespace ACG_1
{
    public partial class MainWindow : Window
    {
        int screenWidth;
        int screenHeight;
        float modelMaxX, modelMaxY, modelMaxZ;
        float modelMinX, modelMinY, modelMinZ;
        float modelSizeX, modelSizeY, modelSizeZ;

        FastBitmap fastBuffer;
        BitmapSource bitmapSource;
        float[] zBuffer;

        Camera camera;
        LightSource lightSource;
        Primitive model;
        Primitive axes;

        System.Drawing.Color ambientColor = System.Drawing.Color.DarkGoldenrod;
        //System.Drawing.Color diffuseColorw = System.Drawing.Color.LightSteelBlue;
        System.Drawing.Color specularColor = System.Drawing.Color.LightYellow;

        //String modelsPath = "c:\\Users\\ASUS N580\\source\\repos\\AKG_Models\\";
        String modelsPath = "d:\\models\\";
        //String modelName = "Plane1";
        //String modelName = "Robot Steampunk";
        String modelName = "Shovel Knight";

        public MainWindow()
        {
            InitializeComponent();
            window.KeyDown += Grid_KeyDown;

            screenWidth = (int)window.Width;
            screenHeight = (int)window.Height;

            fastBuffer = new FastBitmap((int)screenWidth, (int)screenHeight);
            zBuffer = new float[screenWidth * screenHeight];
            Array.Fill<float>(zBuffer, float.MinValue);

            float fNear = 0.1f;
            float fFar = 120000.0f;
            float fFov = 70.0f; // градусы
            //float fAspectRatio = screenHeight / (float)screenWidth;
            float fFovRad = fFov / 180.0f * MathF.PI;

            //model = ParseObjFile(modelsPath + modelName + "\\model.obj");
            model = ParseObjFile("model.obj");
            float scale = 1 / modelSizeY;
            model.Scale(scale);

            camera = new Camera(new Vector3(0, 0.5f, 2), 0, 0, 0, fFovRad, fNear, fFar, screenWidth, screenHeight);

            lightSource = new LightSource(new Vector3(0, modelSizeY * 2, modelSizeY * 5.0f));

            float axesLenght = Math.Max(Math.Max(modelSizeX, modelSizeY), modelSizeZ) * 0.5f;
            axes = new Axes(new Vector3(0, 0, 0), axesLenght);

            //DispatcherTimer dispatcherTimer = new DispatcherTimer();
            //dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            //dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 5);
            //dispatcherTimer.Start();

            DateTime t1, t2;
            t1 = DateTime.Now;
            Render();
            t2 = DateTime.Now;
            var diff = t2.Subtract(t1).Milliseconds;

        }

        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {

            if (e.Key.ToString().Equals("S"))
            {
                camera.Move(new Vector3(0, 0, 0.2f));
                Render();
            }

            if (e.Key.ToString().Equals("W"))
            {
                camera.Move(new Vector3(0, 0, -0.2f));
                Render();
            }
            if (e.Key.ToString().Equals("A"))
            {
                camera.Move(new Vector3(-0.2f, 0, 0));
                Render();
            }

            if (e.Key.ToString().Equals("D"))
            {
                camera.Move(new Vector3(0.2f, 0, 0));
                Render();
            }

            if (e.Key == Key.Up)
            {
                camera.Rotate(0.2f, Axis.X);
                Render();
            }

            if (e.Key == Key.Down)
            {
                camera.Rotate(-0.2f, Axis.X);
                Render();
            }

            if (e.Key == Key.Left)
            {
                camera.Rotate(0.2f, Axis.Y);
                Render();
            }

            if (e.Key == Key.Right)
            {
                camera.Rotate(-0.2f, Axis.Y);
                Render();
            }
            if (e.Key == Key.Z)
            {
                model.Pivot.Rotate(-0.2f, Axis.Y);
                Render();
            }

            if (e.Key == Key.C)
            {
                model.Pivot.Rotate(0.2f, Axis.Y);
                Render();
            }
        }

        public void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            Render();
        }

        public void Render()
        {
            fastBuffer.Clear();
            Array.Fill<float>(zBuffer, float.MaxValue);
            DrawObject(model, camera);
            //DrawObject(axes, camera);
        }

        public bool isPolygonVisible(Vector4 v1, Vector4 v2, Vector4 v3)
        {
            // > 0 clockwise; < 0 counterclockwise
            return ((v1.X - v3.X) * (v2.Y - v1.Y) - (v2.X - v1.X) * (v1.Y - v3.Y)) <= 0;
        }

        public void DrawObject(Primitive primitive, Camera camera)
        {
            Matrix4x4 viewMatrix = camera.ViewMatrix();
            Matrix4x4 viewModelMatrix = Matrix4x4.Multiply(primitive.Pivot.ModelMatrix(), viewMatrix);
            Matrix4x4 resultMatrix = Matrix4x4.Multiply(Matrix4x4.Multiply(Matrix4x4.Multiply(primitive.Pivot.ModelMatrix(), viewMatrix), camera.ProjectionMatrix), camera.ViewportTransformMatrix);

            Matrix4x4 viewLightSourceMatrix = Matrix4x4.Multiply(lightSource.Pivot.ModelMatrix(), viewMatrix);
            Vector3 viewLS = Vector3.Transform(lightSource.Pivot.Center, viewLightSourceMatrix);

            int polygonCount = primitive.VerticesIndexes.Length / 3;
            //for (int i = 0; i < plygonCount; i++)
            Parallel.For(0, polygonCount, new ParallelOptions
            {
                MaxDegreeOfParallelism = 4
            }, (i, state) =>
            {
                int ind1 = i * 3;
                int ind2 = ind1 + 1;
                int ind3 = ind2 + 1;
                // вершины полигона
                Vector3 v1 = primitive.LocalVertices[primitive.VerticesIndexes[ind1] - 1];
                Vector3 v2 = primitive.LocalVertices[primitive.VerticesIndexes[ind2] - 1];
                Vector3 v3 = primitive.LocalVertices[primitive.VerticesIndexes[ind3] - 1];

                Vector3 view1 = Vector3.Transform(v1, viewModelMatrix);
                Vector3 view2 = Vector3.Transform(v2, viewModelMatrix);
                Vector3 view3 = Vector3.Transform(v3, viewModelMatrix);

                if ((view1.Z > -camera.Znear) || (view2.Z > -camera.Znear) || (view3.Z > -camera.Znear)) return;

                Vector4 p1 = VectorMath.Transform(v1, resultMatrix);
                Vector4 p2 = VectorMath.Transform(v2, resultMatrix);
                Vector4 p3 = VectorMath.Transform(v3, resultMatrix);

                //рисуем полигон
                if (isPolygonVisible(p1, p2, p3))
                {
                    if (!IsPointInScreen(p1) && !IsPointInScreen(p2) && !IsPointInScreen(p3)) { return; }
                    Vector2 texCoord1 = primitive.Texture.TextureCoords[primitive.Texture.TextureIndexes[ind1] - 1];
                    Vector2 texCoord2 = primitive.Texture.TextureCoords[primitive.Texture.TextureIndexes[ind2] - 1];
                    Vector2 texCoord3 = primitive.Texture.TextureCoords[primitive.Texture.TextureIndexes[ind3] - 1];
                    DrawFilledTriangle(p1, p2, p3, view1, view2, view3, texCoord1, texCoord2, texCoord3, viewLS, ambientColor, specularColor, viewModelMatrix);
                }
            });
            bitmapSource = Convert(fastBuffer.Bitmap);
            image.Source = bitmapSource;
        }

        public void DrawFilledTriangle(Vector4 p1, Vector4 p2, Vector4 p3,
                                        Vector3 view1, Vector3 view2, Vector3 view3,
                                        Vector2 texCoord1, Vector2 texCoord2, Vector2 texCoord3,
                                        Vector3 LS,
                                        System.Drawing.Color ambientColor, System.Drawing.Color specularColor,
                                        Matrix4x4 viewModelMatrix)
        {
            view1 *= p1.W;
            view2 *= p2.W;
            view3 *= p3.W;

            texCoord1 *= p1.W;
            texCoord2 *= p2.W;
            texCoord3 *= p3.W;

            if (p2.Y < p1.Y)
            {
                (p2, p1) = (p1, p2);
                (view2, view1) = (view1, view2);
                (texCoord2, texCoord1) = (texCoord1, texCoord2);
            }
            if (p3.Y < p1.Y)
            {
                (p3, p1) = (p1, p3);
                (view3, view1) = (view1, view3);
                (texCoord3, texCoord1) = (texCoord1, texCoord3);
            }
            if (p3.Y < p2.Y)
            {
                (p3, p2) = (p2, p3);
                (view3, view2) = (view2, view3);
                (texCoord3, texCoord2) = (texCoord2, texCoord3);
            }

            p1.X = (int)p1.X;
            p1.Y = (int)p1.Y;
            p2.X = (int)p2.X;
            p2.Y = (int)p2.Y;
            p3.X = (int)p3.X;
            p3.Y = (int)p3.Y;

            int total_height = (int)(p3.Y - p1.Y);

            int y, xMax, xMin, segment_height, idx;
            Vector3 A, B, P, N, R, lightingVector, viewVector;
            Vector2 ATex, BTex, Tex;
            float alpha, beta, phi, diffuseLighting, specularLighting;
            bool second_half;
            Vector4 AScreen, BScreen, PScreen;

            int ambientColorR = (int)(ambientColor.R * 0.1f);
            int ambientColorG = (int)(ambientColor.G * 0.1f);
            int ambientColorB = (int)(ambientColor.B * 0.1f);
            int specularColorR = (int)(specularColor.R * 0.9f);
            int specularColorG = (int)(specularColor.G * 0.9f );
            int specularColorB = (int)(specularColor.B * 0.9f);
            int r, g, b;

            System.Drawing.Color pixelColor;

            for (int i = 0; i < total_height; i++)
            {
                y = (int)p1.Y + i;
                if (y >= screenHeight) continue;
                second_half = i > p2.Y - p1.Y || p2.Y == p1.Y;
                segment_height = (int)(second_half ? p3.Y - p2.Y : p2.Y - p1.Y);

                //Коэф интерполяции по всему треугольнику
                alpha = i / (float)total_height;
                AScreen = p1 + (p3 - p1) * alpha;

                A = view1 + (view3 - view1) * alpha;
                ATex = texCoord1 + (texCoord3 - texCoord1) * alpha;

                //Коэф интерполяции по сегменту
                beta = (float)(i - (second_half ? p2.Y - p1.Y : 0)) / segment_height; // be careful: with above conditions no division by zero here
                if (second_half)
                {
                    BScreen = p2 + (p3 - p2) * beta;
                    B = view2 + (view3 - view2) * beta;
                    BTex = texCoord2 + (texCoord3 - texCoord2) * beta;
                }
                else
                {
                    BScreen = p1 + (p2 - p1) * beta;
                    B = view1 + (view2 - view1) * beta;
                    BTex = texCoord1 + (texCoord2 - texCoord1) * beta;
                }

                if (AScreen.X > BScreen.X)
                {
                    (AScreen, BScreen) = (BScreen, AScreen);
                    (A, B) = (B, A);
                    (ATex, BTex) = (BTex, ATex);
                }

                AScreen.X = (int)(AScreen.X);
                BScreen.X = (int)(BScreen.X);

                if ((BScreen.X <= 0) || (y < 0)) continue;
                xMax = BScreen.X >= screenWidth ? screenWidth - 1 : (int)BScreen.X;
                xMin = AScreen.X < 0 ? 0 : (int)AScreen.X;
                for (int x = xMin; x <= xMax; x++)
                {
                    //Коэф интерполяции на отрезке А_Х В_Х
                    phi = BScreen.X == AScreen.X ? 1.0f : (x - AScreen.X) / (float)(BScreen.X - AScreen.X);
                    P = A + (B - A) * phi;
                    PScreen = AScreen + (BScreen - AScreen) * phi;
                    Tex = ATex + (BTex - ATex) * phi; // (1-t) u + tv;
                    Tex /= PScreen.W;

                    P /= PScreen.W;

                    idx = x + y * screenWidth;
                    //P_Z = 1.0f / P_Z;
                    if (zBuffer[idx] > PScreen.Z)
                    {
                        zBuffer[idx] = PScreen.Z;
                        N = model.Texture.GetNormal(Tex.X, Tex.Y);
                        N = Vector3.Transform(N, viewModelMatrix);
                        //Преобразование нормали в координаты наблюдателя
                        Vector3 zero = Vector3.Transform(Vector3.Zero, viewModelMatrix);
                        N -= zero;

                        // diffuse lighting
                        lightingVector = Vector3.Normalize(LS - P);
                        diffuseLighting = VectorMath.Cross(lightingVector, N);
                        if ((diffuseLighting < 0) || (float.IsNaN(diffuseLighting))) diffuseLighting = 0;
                        if (diffuseLighting > 1.0f) diffuseLighting = 1.0f;

                        //specular lighting
                        viewVector = Vector3.Normalize(P - Vector3.Zero);
                        R = Vector3.Normalize(lightingVector - 2 * diffuseLighting * N);
                        specularLighting = VectorMath.Cross(R, viewVector);
                        if (specularLighting < 0) specularLighting = 0;
                        specularLighting = MathF.Pow(specularLighting, 16f);

                        int[] diffuseColor = model.Texture.GetDiffuseMapColor(Tex.X, Tex.Y);
                        float specularCoef = model.Texture.GetSpecularMapCoef(Tex.X, Tex.Y);

                        r = ambientColorR + (int)(diffuseColor[0] * diffuseLighting) + (int)(specularColorR * specularLighting * specularCoef);
                        g = ambientColorG + (int)(diffuseColor[1] * diffuseLighting) + (int)(specularColorG * specularLighting * specularCoef);
                        b = ambientColorB + (int)(diffuseColor[2] * diffuseLighting) + (int)(specularColorB * specularLighting * specularCoef);

                        if (r > 255) r = 255;
                        if (g > 255) g = 255;
                        if (b > 255) b = 255;

                        pixelColor = System.Drawing.Color.FromArgb(r, g, b);
                        printPixel(x, y, pixelColor);
                    }
                }
            }
        }

        public bool IsPointInScreen(Vector4 p)
        {
            return (p.X >= 0) && (p.X < screenWidth) && (p.Y >= 0) && (p.Y < screenHeight);
        }

        public static BitmapSource Convert(System.Drawing.Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                PixelFormats.Bgra32, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);

            return bitmapSource;
        }

        public void printPixel(int x, int y, System.Drawing.Color color)
        {
            fastBuffer.SetPixel(x, y, color.ToArgb());
        }

        public Primitive ParseObjFile(String filename)
        {
            StreamReader reader = new StreamReader(filename);
            String line = "";
            List<String> elements = new List<string>();
            List<Vector3> lv = new List<Vector3>();
            List<int> verticesIdx = new List<int>();
            List<int> normalsIdx = new List<int>();
            List<int> textureIdx = new List<int>();
            List<Vector3> vn = new List<Vector3>();
            List<Vector2> vt = new List<Vector2>();
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Replace("  ", " ");
                elements = line.Trim().Split(" ").ToList();
                if (elements.ElementAt(0).Equals("v"))
                {
                    float x = float.Parse(elements.ElementAt(1), CultureInfo.InvariantCulture);
                    float y = float.Parse(elements.ElementAt(2), CultureInfo.InvariantCulture);
                    float z = float.Parse(elements.ElementAt(3), CultureInfo.InvariantCulture);
                    lv.Add(new Vector3(x, y, z));
                    if (x > modelMaxX) modelMaxX = x;
                    if (y > modelMaxY) modelMaxY = y;
                    if (z > modelMaxZ) modelMaxZ = z;
                    if (x < modelMinX) modelMinX = x;
                    if (y < modelMinY) modelMinY = y;
                    if (z < modelMinZ) modelMinZ = z;

                }
                else if (elements.ElementAt(0).Equals("f"))
                {
                    for (int i = 2; i < elements.Count - 1; i++)
                    {
                        verticesIdx.Add(int.Parse(elements.ElementAt(1).Split("/").ElementAt(0)));
                        verticesIdx.Add(int.Parse(elements.ElementAt(i).Split("/").ElementAt(0)));
                        verticesIdx.Add(int.Parse(elements.ElementAt(i + 1).Split("/").ElementAt(0)));

                        textureIdx.Add(int.Parse(elements.ElementAt(1).Split("/").ElementAt(1)));
                        textureIdx.Add(int.Parse(elements.ElementAt(i).Split("/").ElementAt(1)));
                        textureIdx.Add(int.Parse(elements.ElementAt(i + 1).Split("/").ElementAt(1)));

                        normalsIdx.Add(int.Parse(elements.ElementAt(1).Split("/").ElementAt(2)));
                        normalsIdx.Add(int.Parse(elements.ElementAt(i).Split("/").ElementAt(2)));
                        normalsIdx.Add(int.Parse(elements.ElementAt(i + 1).Split("/").ElementAt(2)));
                    }

                }
                else if (elements.ElementAt(0).Equals("vn"))
                {
                    float x = float.Parse(elements.ElementAt(1), CultureInfo.InvariantCulture);
                    float y = float.Parse(elements.ElementAt(2), CultureInfo.InvariantCulture);
                    float z = float.Parse(elements.ElementAt(3), CultureInfo.InvariantCulture);
                    vn.Add(new Vector3(x, y, z));
                }
                else if (elements.ElementAt(0).Equals("vt"))
                {
                    float x = float.Parse(elements.ElementAt(1), CultureInfo.InvariantCulture);
                    float y = float.Parse(elements.ElementAt(2), CultureInfo.InvariantCulture);
                    vt.Add(new Vector2(x, y));
                }
            }
            modelSizeX = modelMaxX - modelMinX;
            modelSizeY = modelMaxY - modelMinY;
            modelSizeZ = modelMaxZ - modelMinZ;
            reader.Close();
            Texture texture = new Texture(modelsPath + modelName, vt.ToArray(), textureIdx.ToArray());
            return new Primitive(lv.ToArray(), verticesIdx.ToArray(), vn.ToArray(), normalsIdx.ToArray(), texture, new Pivot(new Vector3(0, 0, 0), 0, 0, 0));
        }
    }
}
