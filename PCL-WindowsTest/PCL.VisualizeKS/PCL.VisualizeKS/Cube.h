#include <vector>
#include "Point3D.h"
#pragma once

class Cube
{
public:
	Point3D _min;
	Point3D _max;

	std::vector<Point3D> points;
	Cube();
	bool PointIn(Point3D point);
	Point3D GetMinCoords();
	Point3D GetMaxCoords();
	void Prepare();
};

