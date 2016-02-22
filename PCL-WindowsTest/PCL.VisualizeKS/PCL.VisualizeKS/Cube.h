#include <vector>
#include "Point3D.h"
#pragma once

class Cube
{
	
public:
	std::vector<Point3D> points;
	Cube();
	bool PointIn(Point3D point);
	Point3D GetMinCoords();
	Point3D GetMaxCoords();
};

