#include "stdafx.h"
#define NOMINMAX
#include <Windows.h>
#include <Kinect.h>
#include <pcl/io/pcd_io.h>
#include <pcl/visualization/cloud_viewer.h>
#include <pcl/filters/passthrough.h>
#include <pcl/filters/statistical_outlier_removal.h>
#include <pcl/point_types.h>
#include <pcl/filters/voxel_grid.h>
#include <pcl/conversions.h>
#include <pcl/point_types_conversion.h>
#include "Point3D.h"
#include "Cube.h"

template<class Interface>
inline void SafeRelease(Interface *& pInterfaceToRelease)
{
	if (pInterfaceToRelease != NULL){
		pInterfaceToRelease->Release();
		pInterfaceToRelease = NULL;
	}
}

int _tmain(int argc, _TCHAR* argv[])
{
	// Create Sensor Instance
	IKinectSensor* pSensor;
	HRESULT hResult = S_OK;
	hResult = GetDefaultKinectSensor(&pSensor);
	if (FAILED(hResult)){
		std::cerr << "Error : GetDefaultKinectSensor" << std::endl;
		return -1;
	}

	// Open Sensor
	hResult = pSensor->Open();
	if (FAILED(hResult)){
		std::cerr << "Error : IKinectSensor::Open()" << std::endl;
		return -1;
	}

	// Retrieved Coordinate Mapper
	ICoordinateMapper* pCoordinateMapper;
	hResult = pSensor->get_CoordinateMapper(&pCoordinateMapper);
	if (FAILED(hResult)){
		std::cerr << "Error : IKinectSensor::get_CoordinateMapper()" << std::endl;
		return -1;
	}

	// Retrieved Color Frame Source
	IColorFrameSource* pColorSource;
	hResult = pSensor->get_ColorFrameSource(&pColorSource);
	if (FAILED(hResult)){
		std::cerr << "Error : IKinectSensor::get_ColorFrameSource()" << std::endl;
		return -1;
	}

	// Retrieved Depth Frame Source
	IDepthFrameSource* pDepthSource;
	hResult = pSensor->get_DepthFrameSource(&pDepthSource);
	if (FAILED(hResult)){
		std::cerr << "Error : IKinectSensor::get_DepthFrameSource()" << std::endl;
		return -1;
	}

	// Open Color Frame Reader
	IColorFrameReader* pColorReader;
	hResult = pColorSource->OpenReader(&pColorReader);
	if (FAILED(hResult)){
		std::cerr << "Error : IColorFrameSource::OpenReader()" << std::endl;
		return -1;
	}

	// Open Depth Frame Reader
	IDepthFrameReader* pDepthReader;
	hResult = pDepthSource->OpenReader(&pDepthReader);
	if (FAILED(hResult)){
		std::cerr << "Error : IDepthFrameSource::OpenReader()" << std::endl;
		return -1;
	}

	// Retrieved Color Frame Size
	IFrameDescription* pColorDescription;
	hResult = pColorSource->get_FrameDescription(&pColorDescription);
	if (FAILED(hResult)){
		std::cerr << "Error : IColorFrameSource::get_FrameDescription()" << std::endl;
		return -1;
	}
	int colorWidth = 0;
	int colorHeight = 0;
	pColorDescription->get_Width(&colorWidth); // 1920
	pColorDescription->get_Height(&colorHeight); // 1080

	// To Reserve Color Frame Buffer
	std::vector<RGBQUAD> colorBuffer(colorWidth * colorHeight);

	// Retrieved Depth Frame Size
	IFrameDescription* pDepthDescription;
	hResult = pDepthSource->get_FrameDescription(&pDepthDescription);
	if (FAILED(hResult)){
		std::cerr << "Error : IDepthFrameSource::get_FrameDescription()" << std::endl;
		return -1;
	}
	int depthWidth = 0;
	int depthHeight = 0;
	pDepthDescription->get_Width(&depthWidth); // 512
	pDepthDescription->get_Height(&depthHeight); // 424

	// To Reserve Depth Frame Buffer
	std::vector<UINT16> depthBuffer(depthWidth * depthHeight);

	// Create Cloud Viewer
	pcl::visualization::CloudViewer viewer("Point Cloud Viewer");

	Cube cube = Cube();
	cube.points = {
		Point3D(-0.12, 0.089, -0.009),
		Point3D(0.25, 0.089, -0.009),
		Point3D(-0.12, -0.25, -0.009),
		Point3D(0.25, -0.25, -0.009),

		Point3D(-0.12, 0.089, 0.870001),
		Point3D(0.25, 0.089,  0.870001),
		Point3D(-0.12, -0.25, 0.870001),
		Point3D(0.25, -0.25,  0.870001),
	};

	float x_1 = -0.12, x_2 = 0.25;
	float y_1 = -0.25, y_2 = 0.089;
	float z_1 = -0.009, z_2 = 0.870001;
	float step = 0.01;
	
	int nK = 25, kStep = 1;
	bool log = false;

	while (!viewer.wasStopped()){
		// Acquire Latest Color Frame
		IColorFrame* pColorFrame = nullptr;
		hResult = pColorReader->AcquireLatestFrame(&pColorFrame);
		if (SUCCEEDED(hResult)){
			// Retrieved Color Data
			hResult = pColorFrame->CopyConvertedFrameDataToArray(colorBuffer.size() * sizeof(RGBQUAD), reinterpret_cast<BYTE*>(&colorBuffer[0]), ColorImageFormat::ColorImageFormat_Bgra);
			if (FAILED(hResult)){
				std::cerr << "Error : IColorFrame::CopyConvertedFrameDataToArray()" << std::endl;
			}
		}
		SafeRelease(pColorFrame);

		// Acquire Latest Depth Frame
		IDepthFrame* pDepthFrame = nullptr;
		hResult = pDepthReader->AcquireLatestFrame(&pDepthFrame);
		if (SUCCEEDED(hResult)){
			// Retrieved Depth Data
			hResult = pDepthFrame->CopyFrameDataToArray(depthBuffer.size(), &depthBuffer[0]);
			if (FAILED(hResult)){
				std::cerr << "Error : IDepthFrame::CopyFrameDataToArray()" << std::endl;
			}
		}
		SafeRelease(pDepthFrame);

		// Create Point Cloud
		pcl::PointCloud<pcl::PointXYZ>::Ptr pointcloud(new pcl::PointCloud<pcl::PointXYZ>());

		pointcloud->width = static_cast<uint32_t>(depthWidth);
		pointcloud->height = static_cast<uint32_t>(depthHeight);
		pointcloud->is_dense = true;

		for (int y = 0; y < depthHeight; y++){
			for (int x = 0; x < depthWidth; x++){
				pcl::PointXYZ point;

				DepthSpacePoint depthSpacePoint = { static_cast<float>(x), static_cast<float>(y) };
				UINT16 depth = depthBuffer[y * depthWidth + x];


				// Coordinate Mapping Depth to Camera Space, and Setting PointCloud XYZ
				CameraSpacePoint cameraSpacePoint = { 0.0f, 0.0f, 0.0f };
				pCoordinateMapper->MapDepthPointToCameraSpace(depthSpacePoint, depth, &cameraSpacePoint);
				/*if ((0 <= colorX) && (colorX < colorWidth) && (0 <= colorY) && (colorY < colorHeight)){
					point.x = cameraSpacePoint.X;
					point.y = cameraSpacePoint.Y;
					point.z = cameraSpacePoint.Z;
					}*/

				point.x = cameraSpacePoint.X;
				point.y = cameraSpacePoint.Y;
				point.z = cameraSpacePoint.Z;

				Point3D aux = Point3D(point.x, point.y, point.z);
				if (point.x >= x_1 && point.x <= x_2 &&
					point.y >= y_1 && point.y <= y_2 &&
					point.z >= z_1 && point.z <= 2) {
					if (log) {
						cout << aux._x;
						cout << ", ";
						cout << aux._y;
						cout << ", ";
						cout << aux._z;
						cout << ", ";
					}
					pointcloud->push_back(point);
				}
			}
		}

		/*
			X filters limits:
				V: 0x56
				B: 0x42
			Y filters limits:
				A: 0x41
				S: 0x53
			Z filter limits:
				Z: 0x5A
				X: 0x58
			*/

		if (GetAsyncKeyState(0x56) & 0x8000) {
			if (GetAsyncKeyState(VK_SHIFT))
				x_1 -= step;
			else
				x_1 += step;
		}
		if (GetAsyncKeyState(0x42) & 0x8000) {
			if (GetAsyncKeyState(VK_SHIFT))
				x_2 -= step;
			else
				x_2 += step;
		}

		if (GetAsyncKeyState(0x41) & 0x8000) {
			if (GetAsyncKeyState(VK_SHIFT))
				y_1 -= step;
			else
				y_1 += step;
		}
		if (GetAsyncKeyState(0x53) & 0x8000) {
			if (GetAsyncKeyState(VK_SHIFT))
				y_2 -= step;
			else
				y_2 += step;
		}

		if (GetAsyncKeyState(0x5A) & 0x8000) {
			if (GetAsyncKeyState(VK_SHIFT))
				z_1 -= step;
			else
				z_1 += step;
		}
		if (GetAsyncKeyState(0x58) & 0x8000) {
			if (GetAsyncKeyState(VK_SHIFT))
				z_2 -= step;
			else
				z_2 += step;
		}

		if (GetAsyncKeyState(0x4E)) { // N
			if (GetAsyncKeyState(VK_SHIFT))
				nK -= kStep;
			else
				nK += kStep;
		}

		if (GetAsyncKeyState(VK_SPACE)) {
			log = !log;
		}

		if (GetAsyncKeyState(0x44)) {
			cout << "x: ";
			cout << x_1;
			cout << ", ";
			cout << x_2;
			cout << "  y: ";
			cout << y_1;
			cout << ", ";
			cout << y_2;
			cout << "  z: ";
			cout << z_1;
			cout << ", ";
			cout << z_2;
			cout << " nK: ";
			cout << nK;
			cout << "\n";
		}

		//pcl::PointCloud<pcl::PointXYZ>::Ptr cloudFilteredZ(new pcl::PointCloud<pcl::PointXYZ>());
		// Create the filtering object
		//pcl::PassThrough<pcl::PointXYZ> pass;
		//pass.setInputCloud(pointcloud);
		//pass.setFilterFieldName("z");
		//pass.setFilterLimits(z_1, z_2);
		//pass.filter(*cloudFilteredZ);

		//pcl::PointCloud<pcl::PointXYZ>::Ptr cloudFilteredX(new pcl::PointCloud<pcl::PointXYZ>());
		//pcl::PassThrough<pcl::PointXYZ> passX;
		//passX.setInputCloud(cloudFilteredZ);
		//passX.setFilterFieldName("x");
		//passX.setFilterLimits(x_1, x_2);
		//passX.filter(*cloudFilteredX);

		//pcl::PointCloud<pcl::PointXYZ>::Ptr cloudFilteredY(new pcl::PointCloud<pcl::PointXYZ>());
		//pcl::PassThrough<pcl::PointXYZ> passY;
		//passY.setInputCloud(cloudFilteredX);
		//passY.setFilterFieldName("y");
		//passY.setFilterLimits(y_1, y_2);
		//passY.filter(*cloudFilteredY);

		//pcl::PCLPointCloud2::Ptr pointCloudConverted(new pcl::PCLPointCloud2());
		//pcl::PCLPointCloud2::Ptr cloud_filtered(new pcl::PCLPointCloud2());
		//pcl::toPCLPointCloud2(*cloudFilteredY, *pointCloudConverted);
		//pcl::VoxelGrid<pcl::PCLPointCloud2> sor;
		//sor.setInputCloud(pointCloudConverted);
		//sor.setLeafSize(0.01f, 0.01f, 0.01f);
		//sor.filter(*cloud_filtered);


		//pcl::PointCloud<pcl::PointXYZ>::Ptr aux(new pcl::PointCloud<pcl::PointXYZ>());
		//pcl::fromPCLPointCloud2(*cloud_filtered, *aux);
		//// Show Point Cloud on Cloud Viewer
		viewer.showCloud(pointcloud);

		// Input Key ( Exit ESC key )
		if (GetKeyState(VK_ESCAPE) < 0){
			break;
		}
	}

	// End Processing
	SafeRelease(pColorSource);
	SafeRelease(pDepthSource);
	SafeRelease(pColorReader);
	SafeRelease(pDepthReader);
	SafeRelease(pColorDescription);
	SafeRelease(pDepthDescription);
	SafeRelease(pCoordinateMapper);
	if (pSensor){
		pSensor->Close();
	}
	SafeRelease(pSensor);

	return 0;
}