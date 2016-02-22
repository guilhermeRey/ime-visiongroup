#include "stdafx.h"
#include "Point3D.h"
#include "Cube.h"


Cube::Cube()
{

}

bool Cube::PointIn(Point3D point) {
	Point3D min = GetMinCoords();
	Point3D max = GetMaxCoords();

	return min._x <= point._x <= max._x &&
		min._y <= point._y <= max._y &&
		min._z <= point._z <= max._z;
}

Point3D Cube::GetMaxCoords() {
	Point3D maxCoordsPoint = Point3D();
	maxCoordsPoint._x = points[0]._x;
	maxCoordsPoint._y = points[0]._y;
	maxCoordsPoint._z = points[0]._z;

	for (int i = 0; i < points.size(); i++) {
		if (points[i]._x >= maxCoordsPoint._x)
			maxCoordsPoint._x = points[i]._x;

		if (points[i]._y >= maxCoordsPoint._y)
			maxCoordsPoint._y = points[i]._y;

		if (points[i]._z >= maxCoordsPoint._z)
			maxCoordsPoint._z = points[i]._z;
	}

	return maxCoordsPoint;
}

Point3D Cube::GetMinCoords() {
	Point3D minCoordsPoint = Point3D();
	minCoordsPoint._x = points[0]._x;
	minCoordsPoint._y = points[0]._y;
	minCoordsPoint._z = points[0]._z;

	for (int i = 0; i < points.size(); i++) {
		if (points[i]._x <= minCoordsPoint._x)
			minCoordsPoint._x = points[i]._x;

		if (points[i]._y <= minCoordsPoint._y)
			minCoordsPoint._y = points[i]._y;

		if (points[i]._z <= minCoordsPoint._z)
			minCoordsPoint._z = points[i]._z;
	}

	return minCoordsPoint;
}