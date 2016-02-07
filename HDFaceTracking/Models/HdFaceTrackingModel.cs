using Microsoft.Kinect;
using Microsoft.Kinect.Face;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace HDFace3dTracking.Models
{
    class HdFaceTrackingModel : INotifyPropertyChanged
    {
        Image Back;
        public HdFaceTrackingModel(Image b)
        {
            this.Back = b;
        }
        #region "変数"

        /// <summary>
        /// Kinectセンサーとの接続を示します
        /// </summary>
        private KinectSensor kinect;

        /// <summary>
        /// Kinectセンサーから複数のデータを受け取るためのFrameReaderを示します
        /// </summary>
        private MultiSourceFrameReader reader;

        /// <summary>
        /// Kinectセンサーから取得した骨格情報を示します
        /// </summary>
        private Body[] bodies;

        private FaceAlignment faceAlignment = null;
        private FaceModel faceModel = null;

        private FaceModelBuilderAttributes DefaultAttributes = FaceModelBuilderAttributes.SkinColor | FaceModelBuilderAttributes.HairColor;

        /// <summary>
        /// 顔情報データの取得元を示します
        /// </summary>
        private HighDefinitionFaceFrameSource hdFaceFrameSource = null;

        /// <summary>
        /// 顔情報データを受け取るためのFrameReaderを示します
        /// </summary>
        private HighDefinitionFaceFrameReader hdFaceFrameReader = null;

        /// <summary>
        /// 顔情報を使用して3Dモデルを作成するModelBuilderを示します
        /// </summary>
        private FaceModelBuilder faceModelBuilder = null;


        ColorFrameReader _colorReader = null;


        #endregion

        #region "プロパティ"
        private MeshGeometry3D _Geometry3d;
        /// <summary>
        /// Kinectセンサーから取得した顔情報のMeshを示します
        /// </summary>
        public MeshGeometry3D Geometry3d
        {
            get { return this._Geometry3d; }
            set
            {
                this._Geometry3d = value;
                OnPropertyChanged();
            }
        }

        private string _FaceModelBuilderStatus;
        /// <summary>
        /// FaceModelBuilderのモデル状態を示します
        /// </summary>
        public string FaceModelBuilderStatus
        {
            get { return this._FaceModelBuilderStatus; }
            set
            {
                this._FaceModelBuilderStatus = value;
                OnPropertyChanged();
            }
        }

        private string _FaceModelCaptureStatus;
        /// <summary>
        /// FaceModelBuilderの取得状況を示します
        /// </summary>
        public string FaceModelCaptureStatus
        {
            get { return this._FaceModelCaptureStatus; }
            set
            {
                this._FaceModelCaptureStatus = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Kinectセンサーから取得した顔の状態を示します
        /// </summary>
        public IReadOnlyDictionary<FaceShapeAnimations, float> AnimationUnits
        {
            get
            {
                if (this.faceAlignment == null) return null;
                return this.faceAlignment.AnimationUnits; 
            }
        }

        private Color _SkinColor;
        /// <summary>
        /// 現在のFaceModelのSkinColorを示します
        /// </summary>
        public Color SkinColor
        {
            get { return this._SkinColor; }
            set
            {
                this._SkinColor = value;
                OnPropertyChanged();
            }
        }


        private Color _HairColor;
        /// <summary>
        /// 現在のFaceModelのHairColorを示します
        /// </summary>
        public Color HairColor
        {
            get { return this._HairColor; }
            set
            {
                this._HairColor = value;
                OnPropertyChanged();
            }
        }

        #endregion

        /// <summary>
        /// センサーからの情報の取得を開始します
        /// </summary>
        public void Start()
        {
            Initialize();
        }

        /// <summary>
        /// センサーからの情報の取得を終了します
        /// </summary>
        public void Stop()
        {
            if (this.reader != null)
                this.reader.Dispose();

            this.hdFaceFrameSource = null;

            if (this.hdFaceFrameReader != null)
                this.hdFaceFrameReader.Dispose();

            if (this.faceModelBuilder != null)
                this.faceModelBuilder.Dispose();

            this.kinect.Close();
            this.kinect = null;
        }

        /// <summary>
        /// Kinectセンサーを初期化し、データの取得用に各種変数を初期化します
        /// </summary>
        private void Initialize()
        {
            // Kinectセンサーを取得
            this.kinect = KinectSensor.GetDefault();

            if (kinect == null) return;

            // KinectセンサーからBody(骨格情報)とColor(色情報)を取得するFrameReaderを作成
            reader = kinect.OpenMultiSourceFrameReader(FrameSourceTypes.Body);
            reader.MultiSourceFrameArrived += OnMultiSourceFrameArrived;

            // Kinectセンサーから詳細なFaceTrackingを行う、ソースとFrameReaderを宣言
            // 1st persion
            this.hdFaceFrameSource = new HighDefinitionFaceFrameSource(this.kinect);
            this.hdFaceFrameSource.TrackingIdLost += this.OnTrackingIdLost;
            
            this.hdFaceFrameReader = this.hdFaceFrameSource.OpenReader();
            this.hdFaceFrameReader.FrameArrived += this.OnFaceFrameArrived;

            this.faceModel = new FaceModel();
            this.faceAlignment = new FaceAlignment();

            
            this._colorReader = this.kinect.ColorFrameSource.OpenReader();
            this._colorReader.FrameArrived += ColorReader_FrameArrived;
            // 各種Viewのアップデート
            InitializeMesh();
            UpdateMesh();

            // センサーの開始
            kinect.Open();
        }

        void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    this.Back.Source = frame.ToBitmap();

                    Canvas.SetLeft(Back, -400);
                }
            }
        }

        /// <summary>
        /// 表示用のMeshを初期化します
        /// </summary>
        private void InitializeMesh()
        {
            // MeshGeometry3Dのインスタンスを作成
            this._Geometry3d = new MeshGeometry3D();

            // Vertexを計算する
            var vertices = this.faceModel.CalculateVerticesForAlignment(this.faceAlignment);

            var triangleIndices = this.faceModel.TriangleIndices;

            var indices = new Int32Collection(2*triangleIndices.Count);

            uint max = 0;
            // 3つの点で1セット
            
            for (int i = 0; i < triangleIndices.Count; i += 3)
            {   
                uint index1 = triangleIndices[i];
                uint index2 = triangleIndices[i + 1];
                uint index3 = triangleIndices[i + 2];
                if (max < index1) max = index1;
                if (max < index2) max = index2;
                if (max < index3) max = index3;
                indices.Add((int)index3);
                indices.Add((int)index2);
                indices.Add((int)index1);
            }
            //Left Eye
            indices.Add(vertices.Count + 0);
            indices.Add(vertices.Count + 1);
            indices.Add(vertices.Count + 2);

            indices.Add(vertices.Count + 2);
            indices.Add(vertices.Count + 3);
            indices.Add(vertices.Count + 0);

            //Right Eye
            indices.Add(vertices.Count + 6);
            indices.Add(vertices.Count + 5);
            indices.Add(vertices.Count+4);

            indices.Add(vertices.Count+4);
            indices.Add(vertices.Count+7);
            indices.Add(vertices.Count+6);

            //Left EyeBrow
            indices.Add(vertices.Count + 8);
            indices.Add(vertices.Count + 9);
            indices.Add(vertices.Count + 10);
            
            //LeftEye
            indices.Add(vertices.Count + 11);
            indices.Add(vertices.Count + 12);
            indices.Add(vertices.Count + 13);

            var offset = vertices.Count + 24;
            
            //MessageBox.Show(max.ToString()+","+vertices.Count.ToString());
            this._Geometry3d.TriangleIndices = indices;
            this._Geometry3d.Normals = null;
            this._Geometry3d.Positions = new Point3DCollection();
            this._Geometry3d.TextureCoordinates = new PointCollection();
           
            for (int i = 0; i < vertices.Count;i++ )
            {
                var vert = vertices[i];
                this._Geometry3d.Positions.Add(new Point3D(vert.X, vert.Y, -vert.Z));
                this._Geometry3d.TextureCoordinates.Add(new Point(1, 1)); 
                
            }
        
            AddPoint(vertices[210], 256, 128);
            AddPoint(vertices[241], 128, 0);
            AddPoint(vertices[469], 0, 128);
            AddPoint(vertices[1104],128, 256);

            AddPoint(vertices[843], 256, 128);
            AddPoint(vertices[731], 128+256, 0);
            AddPoint(vertices[1117], 512, 128);
            AddPoint(vertices[1090], 128+256, 256);

            AddPoint(vertices[346], 140, 286);
            AddPoint(vertices[222], 70, 230);
            AddPoint(vertices[140], 0, 286);

            AddPoint(vertices[758], 0, 286);
            AddPoint(vertices[849], 70, 230);
            AddPoint(vertices[803], 140, 286);
            //AddPoint(vertices[])
            // 表示の更新
            OnPropertyChanged("Geometry3d");
        }

        private void AddPoint(CameraSpacePoint p,int x,int y)
        {
            this._Geometry3d.Positions.Add(new Point3D(p.X, p.Y, -p.Z));
            this._Geometry3d.TextureCoordinates.Add(new Point(x, y));
        }

        private void UpdateFacePoint(CameraSpacePoint p, int index, float dx, float dy, float dz)
        {
            this._Geometry3d.Positions[index] = new Point3D(p.X+dx, p.Y+dy, -p.Z+dz);
        }

        private float DistancePoint(CameraSpacePoint x,CameraSpacePoint y)
        {
            var deltaX = x.X - y.X;
            var deltaY = x.Y - y.Y;
            var deltaZ = x.Z - y.Z;
            return (float) Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
        }
        /// <summary>
        /// Kinectセンサーから取得した情報を用いて表示用のMeshを更新します
        /// </summary>
        private void UpdateMesh()
        {
            var vertices = this.faceModel.CalculateVerticesForAlignment(this.faceAlignment);
            
            for (int i = 0; i < vertices.Count; i++)
            {
                var vert = vertices[i];
                this._Geometry3d.Positions[i] = new Point3D(vert.X, vert.Y, -vert.Z);
            }

            //Update Eyes
            float distance = 0.018f;
            float scaleLeftH = DistancePoint(vertices[241], vertices[1104]);
            float scaleLeftW = DistancePoint(vertices[210], vertices[469]);
            float scaleRightH = DistancePoint(vertices[731], vertices[1090]);
            float scaleRightW = DistancePoint(vertices[843], vertices[1117]);

            float dy = 0.005f;
            //UpdateEyeBrow
            UpdateFacePoint(vertices[346], vertices.Count + 8,   scaleLeftW / 2,              0.0f+dy,     distance);
            UpdateFacePoint(vertices[222], vertices.Count + 9,             0.0f,scaleRightH * 2.0f+dy,     distance);
            UpdateFacePoint(vertices[140], vertices.Count + 10, -scaleLeftW / 4,              0.0f+dy,     distance);

            UpdateFacePoint(vertices[758], vertices.Count + 11, scaleRightW / 4,              0.0f+dy,     distance);
            UpdateFacePoint(vertices[849], vertices.Count + 12,            0.0f,scaleRightH * 2.0f+dy,     distance);
            UpdateFacePoint(vertices[803], vertices.Count + 13,-scaleRightW / 2,              0.0f+dy,     distance);

            //MessageBox.Show(scaleLeftH.ToString());
            scaleLeftH = (scaleLeftH - 0.005f) * 3;
            scaleLeftH = scaleLeftH * 4.0f;
            scaleRightH = (scaleRightH - 0.005f) * 3;
            scaleRightH = scaleRightH * 4.0f;
            
            //MessageBox.Show(vertices[210].Z.ToString());
            UpdateFacePoint(vertices[210], vertices.Count + 0, scaleLeftW, 0.0f, distance);
            UpdateFacePoint(vertices[241], vertices.Count + 1, 0.0f, scaleLeftH, distance);
            UpdateFacePoint(vertices[469], vertices.Count + 2, -scaleLeftW, 0.0f, distance);
            UpdateFacePoint(vertices[1104], vertices.Count + 3, 0.0f, -scaleLeftH, distance);

            UpdateFacePoint(vertices[843], vertices.Count + 4, -scaleRightW, 0.0f, distance);
            UpdateFacePoint(vertices[731], vertices.Count + 5, 0.0f, scaleRightH, distance);
            UpdateFacePoint(vertices[1117], vertices.Count + 6, scaleRightW, 0.0f, distance);
            UpdateFacePoint(vertices[1090], vertices.Count + 7, 0.0f, -scaleRightH, distance);
            //MessageBox.Show(vertices[210].Y.ToString() + "." + vertices[241].Y.ToString());







            OnPropertyChanged("Geometry3d");
        }


        /// <summary>
        /// センサーから骨格データを受け取り処理します
        /// </summary>
        private void OnMultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var frame = e.FrameReference.AcquireFrame();
            if (frame == null) return;

            // BodyFrameに関してフレームを取得する
            using (var bodyFrame = frame.BodyFrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (bodies == null)
                        bodies = new Body[bodyFrame.BodyCount];

                    // 骨格データを格納
                    bodyFrame.GetAndRefreshBodyData(bodies);

                    // FaceTrackingが開始されていないか確認
                    if (!this.hdFaceFrameSource.IsTrackingIdValid)
                    {
                        // トラッキング先の骨格を選択

                        var target = (from body in this.bodies where body.IsTracked select body).FirstOrDefault();
                        if (target != null)
                        {
                            // 検出されたBodyに対してFaceTrackingを行うよう、FaceFrameSourceを設定
                            hdFaceFrameSource.TrackingId = target.TrackingId;
                            // FaceModelBuilderを初期化
                            if (this.faceModelBuilder != null)
                            {
                                this.faceModelBuilder.Dispose();
                                this.faceModelBuilder = null;
                            }
                            this.faceModelBuilder = this.hdFaceFrameSource.OpenModelBuilder(DefaultAttributes);
                            // FaceModelBuilderがモデルの構築を完了した時に発生するイベント
                            this.faceModelBuilder.CollectionCompleted += this.OnModelBuilderCollectionCompleted;
                            // FaceModelBuilderの状態を報告するイベント
                            this.faceModelBuilder.CaptureStatusChanged += faceModelBuilder_CaptureStatusChanged;
                            this.faceModelBuilder.CollectionStatusChanged += faceModelBuilder_CollectionStatusChanged;

                            // キャプチャの開始
                            this.faceModelBuilder.BeginFaceDataCollection();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// FaceModelBuilderの収集状況が変更されたときのイベントを処理します
        /// </summary>
        private void faceModelBuilder_CollectionStatusChanged(object sender, FaceModelBuilderCollectionStatusChangedEventArgs e)
        {
            if (this.faceModelBuilder != null)
                this.FaceModelBuilderStatus = GetCollectionStatus(((FaceModelBuilder)sender).CollectionStatus);
        }

        /// <summary>
        /// FaceModelBuilderの取得状況が変更されたときのイベントを処理します
        /// </summary>
        private void faceModelBuilder_CaptureStatusChanged(object sender, FaceModelBuilderCaptureStatusChangedEventArgs e)
        {
            if (this.faceModelBuilder != null)
                this.FaceModelCaptureStatus = ((FaceModelBuilder)sender).CaptureStatus.ToString();
        }

        /// <summary>
        /// FaceTrackingの対象をロストしたときのイベントを処理します
        /// </summary>
        private void OnTrackingIdLost(object sender, TrackingIdLostEventArgs e)
        {
            // faceReaderとfaceSourceを初期化して次のトラッキングに備える
            //this.isCaptured = false;
            this.hdFaceFrameSource.TrackingId = 0;
        }


        /// <summary>
        /// FaceModelBuilderがモデルの構築を完了したときのイベントを処理します
        /// </summary>
        private void OnModelBuilderCollectionCompleted(object sender, FaceModelBuilderCollectionCompletedEventArgs e)
        {
            var modelData = e.ModelData;
            this.faceModel = modelData.ProduceFaceModel();
            
            // MeshをUpdate
            UpdateMesh();

            // SkinColorとHairColorの値も更新
            this.SkinColor = UIntToColor(this.faceModel.SkinColor);
            this.HairColor = UIntToColor(this.faceModel.HairColor);
            
            // 更新を行う
            this.FaceModelBuilderStatus = GetCollectionStatus(((FaceModelBuilder)sender).CollectionStatus);
            this.FaceModelCaptureStatus = ((FaceModelBuilder)sender).CaptureStatus.ToString();

            this.faceModelBuilder.Dispose();
            this.faceModelBuilder = null;
        }

        /// <summary>
        /// FaceFrameが利用できるようになった時のイベントを処理します
        /// </summary>
        private void OnFaceFrameArrived(object sender, HighDefinitionFaceFrameArrivedEventArgs e)
        {
            using (var faceFrame = e.FrameReference.AcquireFrame())
            {
                
                if (faceFrame == null || !faceFrame.IsFaceTracked) return;

                
                // FaceAlignmentを更新
                faceFrame.GetAndRefreshFaceAlignmentResult(this.faceAlignment);
                UpdateMesh();

                // Animation Unitを更新
                OnPropertyChanged("AnimationUnits");

            }
        }

        /// <summary>
        /// FaceModelBuilderのCollectionStatusを取得します
        /// </summary>
        private string GetCollectionStatus(FaceModelBuilderCollectionStatus status)
        {
            var msgs = new List<string>();

            if ((status & FaceModelBuilderCollectionStatus.FrontViewFramesNeeded) != 0)
                msgs.Add("FrontViewFramesNeeded");

            if ((status & FaceModelBuilderCollectionStatus.LeftViewsNeeded)!= 0)
                msgs.Add("LeftViewsNeeded");

            if ((status & FaceModelBuilderCollectionStatus.MoreFramesNeeded) != 0)
                msgs.Add("MoreFramesNeeded");

            if ((status & FaceModelBuilderCollectionStatus.RightViewsNeeded) != 0)
                msgs.Add("RightViewsNeeded");

            if ((status & FaceModelBuilderCollectionStatus.TiltedUpViewsNeeded) != 0)
                msgs.Add("TiltedUpViewsNeeded");

            if ((status & FaceModelBuilderCollectionStatus.Complete) != 0)
                msgs.Add("Complete!");

            return string.Join(" / ", msgs);
        }

        /// <summary>
        /// UIntの値をColorに変換します
        /// </summary>
        private Color UIntToColor(uint color)
        {
            byte a = (byte)(color >> 24);
            byte r = (byte)(color >> 16);
            byte g = (byte)(color >> 8);
            byte b = (byte)(color >> 0);
            return Color.FromArgb(a, r, g, b);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName]string propertyName = null)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }


    }
}