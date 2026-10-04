import * as THREE from 'three';
export function insidePolygon(point, polygon) {
  let inside=false;
  for(let i=0,j=polygon.length-1;i<polygon.length;j=i++) {
    const a=polygon[i],b=polygon[j];
    if((a.y>point.y)!==(b.y>point.y) && point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
  }
  return inside;
}
export function polygonFaces(mesh,camera,polygon,candidates,through=false) {
  mesh.updateWorldMatrix(true,false);camera.updateMatrixWorld();mesh.skeleton?.update();
  const geometry=mesh.geometry,count=(geometry.index?.count||geometry.attributes.position.count)/3;
  const a=new THREE.Vector3(),b=new THREE.Vector3(),c=new THREE.Vector3(),center=new THREE.Vector3(),projected=new THREE.Vector3(),ray=new THREE.Raycaster();
  const faces=[];
  for(let face=0;face<count;face++) {
    const vertex=corner=>geometry.index?geometry.index.getX(face*3+corner):face*3+corner;
    mesh.getVertexPosition(vertex(0),a);mesh.getVertexPosition(vertex(1),b);mesh.getVertexPosition(vertex(2),c);
    center.copy(a).add(b).add(c).multiplyScalar(1/3).applyMatrix4(mesh.matrixWorld);
    projected.copy(center).project(camera);
    if(projected.z < -1 || projected.z > 1 || !insidePolygon({x:(projected.x+1)/2,y:(1-projected.y)/2},polygon))continue;
    if(!through) {
      ray.setFromCamera(new THREE.Vector2(projected.x,projected.y),camera);
      const hit=ray.intersectObjects(candidates,false)[0];
      if(!hit || hit.object!==mesh || hit.point.distanceTo(center)>Math.max(1e-6,center.distanceTo(camera.position)*1e-5))continue;
    }
    faces.push(face);
  }
  return faces;
}
