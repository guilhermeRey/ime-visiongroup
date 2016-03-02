#include "stdafx.h"
#define NOMINMAX
#include <Windows.h>
#include <Kinect.h>
#include <pcl/io/pcd_io.h>
#include <pcl/visualization/cloud_viewer.h>
#include <pcl/filters/statistical_outlier_removal.h>
#include <pcl/point_types.h>
#include <pcl/filters/voxel_grid.h>
#include <pcl/conversions.h>
#include <pcl/point_types_conversion.h>
#include <pcl/ModelCoefficients.h>
#include <pcl/point_types.h>
#include <pcl/filters/extract_indices.h>
#include <pcl/filters/voxel_grid.h>
#include <pcl/features/normal_3d.h>
#include <pcl/kdtree/kdtree.h>
#include <pcl/sample_consensus/method_types.h>
#include <pcl/sample_consensus/model_types.h>
#include <pcl/segmentation/sac_segmentation.h>
#include <pcl/segmentation/extract_clusters.h>
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

	float x_1 = -0.06, x_2 = 0.21;
	float y_1 = -0.23, y_2 = 0.049;
	float z_1 = 0.471, z_2 = 0.870001;
	float step = 0.001;
	float segThreshold = 0.04;
	
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
		pcl::PointCloud<pcl::PointXYZRGB>::Ptr viewcloud(new pcl::PointCloud<pcl::PointXYZRGB>());
		pcl::PointCloud<pcl::PointXYZ>::Ptr pointcloud(new pcl::PointCloud<pcl::PointXYZ>());
		

		pointcloud->width = static_cast<uint32_t>(depthWidth);
		pointcloud->height = static_cast<uint32_t>(depthHeight);
		pointcloud->is_dense = false;

		viewcloud->width = static_cast<uint32_t>(depthWidth);
		viewcloud->height = static_cast<uint32_t>(depthHeight);
		viewcloud->is_dense = false;

		for (int y = 0; y < depthHeight; y++) {
			for (int x = 0; x < depthWidth; x++) {
				pcl::PointXYZRGB point;
				DepthSpacePoint depthSpacePoint = { static_cast<float>(x), static_cast<float>(y) };
				UINT16 depth = depthBuffer[y * depthWidth + x];

				// Coordinate Mapping Depth to Camera Space, and Setting PointCloud XYZ
				CameraSpacePoint cameraSpacePoint = { 0.0f, 0.0f, 0.0f };
				pCoordinateMapper->MapDepthPointToCameraSpace(depthSpacePoint, depth, &cameraSpacePoint);
				point.x = cameraSpacePoint.X;
				point.y = cameraSpacePoint.Y;
				point.z = cameraSpacePoint.Z;
				
				if (point.x >= x_1 && point.x <= x_2 &&
					point.y >= y_1 && point.y <= y_2 &&
					point.z >= z_1 && point.z <= z_2) {
					point.r = 155;
					point.g = 155;
					point.b = 155;

					pcl::PointXYZ toAddPoint;
					toAddPoint.x = point.x;
					toAddPoint.y = point.y;
					toAddPoint.z = point.z;

					pointcloud->push_back(toAddPoint);
					viewcloud->push_back(point);
				}/*
				else {
					point.r = 55;
					point.g = 55;
					point.b = 55;
				}*/

				//
			}
		}

		//pcl::PCLPointCloud2::Ptr pointCloudConverted(new pcl::PCLPointCloud2());
		//pcl::PCLPointCloud2::Ptr cloud_filtered(new pcl::PCLPointCloud2());
		//pcl::toPCLPointCloud2(*pointcloud, *pointCloudConverted);
		//pcl::VoxelGrid<pcl::PCLPointCloud2> sor;
		//sor.setInputCloud(pointCloudConverted);
		//sor.setLeafSize(0.01f, 0.01f, 0.01f);
		//sor.filter(*cloud_filtered);


		//pcl::PointCloud<pcl::PointXYZ>::Ptr aux(new pcl::PointCloud<pcl::PointXYZ>());
		//pcl::fromPCLPointCloud2(*cloud_filtered, *aux);
		//// Show Point Cloud on Cloud Viewer
		//viewer.showCloud(aux);

		pcl::ModelCoefficients::Ptr coefficients(new pcl::ModelCoefficients);
		pcl::PointIndices::Ptr inliers(new pcl::PointIndices);
		// Create the segmentation object
		pcl::SACSegmentation<pcl::PointXYZ> seg;
		// Optional
		seg.setOptimizeCoefficients(true);
		// Mandatory
		seg.setModelType(pcl::SACMODEL_PLANE);
		seg.setMethodType(pcl::SAC_RANSAC);
		seg.setDistanceThreshold(segThreshold);

		seg.setInputCloud(pointcloud);
		seg.segment(*inliers, *coefficients);
		
		for (size_t i = 0; i < inliers->indices.size(); ++i) {
			viewcloud->points[inliers->indices[i]].r = 0;
			viewcloud->points[inliers->indices[i]].g = 255;
			viewcloud->points[inliers->indices[i]].b = 0;
		}

		viewer.showCloud(viewcloud);

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
		if (GetAsyncKeyState(VK_SPACE)) {
			if (GetAsyncKeyState(VK_SHIFT))
				segThreshold -= step;
			else
				segThreshold += step;
			
			//log = !log;
		}

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