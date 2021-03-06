﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Interfaces;
using Autodesk.DesignScript.Runtime;
using Unfold.Interfaces;
using Unfold.Topology;
using DynamoText;


namespace Unfold
{
    /// <summary>
    /// class that contains the unfolding methods and algorithms that perform unfolding
    /// </summary>
    public static class PlanarUnfolder
    {

        /// <summary>
        /// class that records a set of ids and a coordinate system that these ids
        /// were transformed to
        /// </summary>
        public class FaceTransformMap
        {

			public Plane RotationPlane { get; set; }
            public double RotationDegrees { get; set; }
            public List<int> IDS { get; set; }
			public CoordinateSystem From { get; set; }
			public CoordinateSystem To { get; set; }

			public FaceTransformMap(CoordinateSystem from,CoordinateSystem to, List<int> ids)
			{
				From = from;
				To = to;
				IDS = ids;
			}

            public FaceTransformMap(Plane rotPlane, double rotdegrees, List<int> ids)
            {
                IDS = ids;
				RotationDegrees = rotdegrees;
				RotationPlane = rotPlane;
				From = null;
				To = null;
            }



        }

        /// <summary>
        /// class that represents a single unfolding operation
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="T"></typeparam>
        public class PlanarUnfolding<K, T>
            where T : IUnfoldablePlanarFace<K>
            where K : IUnfoldableEdge
        {
            public List<T> StartingUnfoldableFaces { get; set; }
            public List<List<Surface>> UnfoldedSurfaceSet { get; set; }
            public List<FaceTransformMap> Maps { get; set; }
            public Dictionary<int, Point> StartingPoints { get; set; }
            public List<T> UnfoldedFaces { get; set; }
            public List<GraphVertex<K,T>> OriginalGraph {get; set;}

            /// <summary>
            /// constructor
            /// </summary>
            /// <param name="originalFaces"> the starting IunfoldableFaces</param>
            /// <param name="finalSurfaces"> the unfolded surfaces</param>
            /// <param name="transforms"> the transforms that track all surfaces</param>
            /// <param name="unfoldedfaces">the unfolded IunfoldableFaces</param>
            public PlanarUnfolding(List<T> originalFaces, List<List<Surface>> finalSurfaces, List<FaceTransformMap> transforms, List<T> unfoldedfaces, List<GraphVertex<K,T>> oldGraph)
            {
                Console.WriteLine("generating new unfolding to return");
                StartingUnfoldableFaces = originalFaces.ToList();
                Maps = transforms;
                UnfoldedFaces = unfoldedfaces;
                StartingPoints = StartingUnfoldableFaces.ToDictionary(x => x.ID, x => Tesselation.MeshHelpers.SurfaceAsPolygonCenter(x.SurfaceEntities.First()));
                UnfoldedSurfaceSet = finalSurfaces;
                OriginalGraph = oldGraph;
            }

           /// <summary>
           /// this method attempts to merge unfolding operations so they can be labeled and packed together
           /// </summary>
           /// <typeparam name="K"></typeparam>
           /// <typeparam name="T"></typeparam>
           /// <param name="unfoldingstomerge"></param>
           /// <returns></returns>
            public static PlanarUnfolding<K, T> MergeUnfoldings<K, T>(List<PlanarUnfolding<K, T>> unfoldingstomerge)
                where T : IUnfoldablePlanarFace<K>
                where K : IUnfoldableEdge
            {
                // iterate each unfolding
                var mergedOrgFaces = new List<T>();
                var mergedFinalSurfaces = new List<List<Surface>>();
                var mergedTransformMaps = new List<FaceTransformMap>();
                var mergedUnfoldedFaces = new List<T>();
                var concatedGraphs = new List<GraphVertex<K, T>>();

                for (int i = 0; i < unfoldingstomerge.Count; i++)
                {
                    var currentUnfolding = unfoldingstomerge[i];
                    var indexOffset = Enumerable.Range(0, i).Select(index => unfoldingstomerge[index].StartingUnfoldableFaces.Count).Sum();


                    var modifiedfaces = currentUnfolding.StartingUnfoldableFaces.Select(x => { x.ID = x.ID + indexOffset; return x; }).ToList();
                    mergedOrgFaces.AddRange(modifiedfaces);

                    var currentfinalsurfaces = currentUnfolding.UnfoldedFaces.Select(x => x.SurfaceEntities).ToList();
                    mergedFinalSurfaces.AddRange(currentfinalsurfaces);

                    var modifiedmaps = new List<FaceTransformMap>();
                    foreach (var currentmap in currentUnfolding.Maps)
                    {
                        for (int j = 0; j < currentmap.IDS.Count; j++)
                        {
                            currentmap.IDS[j] = currentmap.IDS[j] + indexOffset;


                        }
                        modifiedmaps.Add(currentmap);
                    }
                    mergedTransformMaps.AddRange(modifiedmaps);

                    
                    var modifedUnfoldedfaces = new List<T>();
                    foreach (var unfoldedFace in currentUnfolding.UnfoldedFaces){
                        unfoldedFace.ID += indexOffset;
                        var modifiedIds = new List<int>();
                        foreach (var id in unfoldedFace.IDS){
                            modifiedIds.Add(id+indexOffset);
                        }
                        unfoldedFace.IDS = modifiedIds;
                        modifedUnfoldedfaces.Add(unfoldedFace);
                    }

                    mergedUnfoldedFaces.AddRange(modifedUnfoldedfaces);
                    concatedGraphs.AddRange(currentUnfolding.OriginalGraph);
                }
                //TODO need to implement an algo that actually merges the graphs so we can add tabs to merged unfolds
                //for now we just concat the list of graphs
                return new PlanarUnfolding<K, T>(mergedOrgFaces, mergedFinalSurfaces, mergedTransformMaps, mergedUnfoldedFaces,concatedGraphs);
            }
        }

        /// <summary>
        /// class that represents a label
        /// contains raw label geometry, geometry aligned to face
        ///  and the ID of the surface
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="T"></typeparam>
        public class UnfoldableFaceLabel<K, T>
            where T : IUnfoldablePlanarFace<K>
            where K : IUnfoldableEdge
        {

            public string Label { get; set; }
            public List<Curve> LabelGeometry { get; set; }
            public List<Curve> AlignedLabelGeometry { get; set; }
            public int ID { get; set; }
            public T UnfoldableFace { get; set; }


            private IEnumerable<Curve> GenLabelGeometryFromId(int id, double scale = 1.0)
            {

                List<Curve> textgeo = Text.FromStringOriginAndScale(id.ToString(), Point.ByCoordinates(0, 0, 0), scale) as List<Curve>;


                return textgeo;

            }

			/// <summary>
			/// align the label geometry to the face that it represents
			/// </summary>
			/// <param name="geo"></param>
			/// <returns></returns>
            private IEnumerable<Curve> AlignGeoToFace(IEnumerable<Curve> geo)
            {
                var oldgeo = new List<DesignScriptEntity>();
                var approxCenter = Tesselation.MeshHelpers.SurfaceAsPolygonCenter(UnfoldableFace.SurfaceEntities.First());
                var norm = UnfoldableFace.SurfaceEntities.First().NormalAtPoint(approxCenter);
                var facePlane = Plane.ByOriginNormal(approxCenter, norm);
                var finalCordSystem = CoordinateSystem.ByPlane(facePlane);
                //a list for collecting old geo that needs to be disposed
                oldgeo.Add(facePlane);
                // find bounding box of set of curves
                var textBoudingBox = BoundingBox.ByGeometry(geo);
                oldgeo.Add(textBoudingBox);
                // find the center of this box and use as start point
                var textCeneter = textBoudingBox.MinPoint.Add((
                    textBoudingBox.MaxPoint.Subtract(textBoudingBox.MinPoint.AsVector())
                    .AsVector().Scale(.5)));

                var transVector = Vector.ByTwoPoints(textCeneter, Point.ByCoordinates(0, 0, 0));
               
                var geoIntermediateTransform = geo.Select(x => x.Translate(transVector)).Cast<Curve>().AsEnumerable();
                oldgeo.AddRange(geoIntermediateTransform);
                var finalTransformedLabel = geoIntermediateTransform.Select(x => x.Transform(finalCordSystem)).Cast<Curve>().AsEnumerable();
                foreach (IDisposable item in oldgeo)
                {
                    item.Dispose();
                }

                return finalTransformedLabel;

            }

            public UnfoldableFaceLabel(T face, double labelscale = 1.0)
            {
                ID = face.ID;
                LabelGeometry = GenLabelGeometryFromId(ID, labelscale).ToList();
                Label = ID.ToString();
                UnfoldableFace = face;
                AlignedLabelGeometry = AlignGeoToFace(LabelGeometry).ToList();

            }



        }

		//following methods take an ID, some geometry and an unfold and attempt to push that geometry through the same
		//operations that were done to the surface with the supplied ID, this lets a label be placed on the resulting 
		//unfolded surface at the correct spot, also used for tabs.
		# region map geo through unfold by transforms

		private static G ApplyTransformations<G>(G geometryToTransform, List<FaceTransformMap> transformMaps) where G: Geometry
		{
			var oldGeometry = new List<G>();
			G aggregatedGeo = geometryToTransform;
			for (int i = 0; i + 1 < transformMaps.Count; i++)
			{
				//if this transformMap is a coordinateSystem, then use geo.transform
				if (transformMaps[i + 1].From != null)
				{
					aggregatedGeo = aggregatedGeo.Transform(transformMaps[i + 1].From, transformMaps[i + 1].To) as G;
				}
				else
				{	var plane = transformMaps[i + 1].RotationPlane;
					var degrees = transformMaps[i + 1].RotationDegrees;
					aggregatedGeo = aggregatedGeo.Rotate(plane,degrees) as G;
				}
				// we only need to keep the last transformation, so add all the others 
				// to the disposal list
				if (i != transformMaps.Count - 2)
				{
					oldGeometry.Add(aggregatedGeo);
				}
			}
			foreach (IDisposable item in oldGeometry)
			{
				item.Dispose();
			}

			return aggregatedGeo;
		}

        private static G ApplyTransformations<G>(G geometryToTransform, List<CoordinateSystem> transforms) where G : Geometry
        {
            //list of geo to dispose at the end of the mapping
            var oldGeometry = new List<G>();
            G aggregatedGeo = geometryToTransform;
            for (int i = 0; i + 1 < transforms.Count; i++)
            {
                aggregatedGeo = aggregatedGeo.Transform(transforms[i + 1]) as G;
                // we only need to keep the last transformation, so add all the others 
                // to the disposal list
                if (i != transforms.Count - 2)
                {
                    oldGeometry.Add(aggregatedGeo);
                }
            }
            foreach (IDisposable item in oldGeometry)
            {
                item.Dispose();
            }

            return aggregatedGeo;
        }

        public static List<G> MapGeometryToUnfoldingByID<K, T, G>(PlanarUnfolder.PlanarUnfolding<K, T> unfolding, List<G> geometryToTransform, int id)
            where T : IUnfoldablePlanarFace<K>
            where K : IUnfoldableEdge
            where G : Geometry
        {
            // find bounding box of set of curves
            var myBox = BoundingBox.ByGeometry(geometryToTransform);

            // find the center of this box and use as start point
            var geoStartPoint = myBox.MinPoint.Add((myBox.MaxPoint.Subtract(myBox.MinPoint.AsVector()).AsVector().Scale(.5)));

            // transform each curve using this new center as an offset so it ends up translated correctly to the surface center
            var transformedgeo = geometryToTransform.Select(x => MapGeometryToUnfoldingByID(unfolding, x, id, geoStartPoint)).ToList();

            return transformedgeo;
        }



        public static List<G> DirectlyMapGeometryToUnfoldingByID<K, T, G>(PlanarUnfolder.PlanarUnfolding<K, T> unfolding, List<G> geometryToTransform, int id)
            where T : IUnfoldablePlanarFace<K>
            where K : IUnfoldableEdge
            where G : Geometry
        {

            var transformedgeo = geometryToTransform.Select(x => DirectlyMapGeometryToUnfoldingByID(unfolding, x, id)).ToList();

            return transformedgeo;
        }
        public static G DirectlyMapGeometryToUnfoldingByID<K, T, G>(PlanarUnfolder.PlanarUnfolding<K, T> unfolding, G geometryToTransform, int id)

            where T : IUnfoldablePlanarFace<K>
            where K : IUnfoldableEdge
            where G : Geometry
        {

            // grab all transforms that were applied to this surface id
            var map = unfolding.Maps;
            var applicableTransforms = map.Where(x => x.IDS.Contains(id)).ToList();
            

            // set the geometry to the first applicable transform
            //geometryToTransform = geometryToTransform.Transform(transforms.First()) as G;

            return ApplyTransformations<G>(geometryToTransform, applicableTransforms);

        }

       

        /// <summary>
        /// overload that automatically calculates startpoint from boundingbox center 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="G"></typeparam>
        /// <param name="unfolding"></param>
        /// <param name="geometryToTransform"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static G MapGeometryToUnfoldingByID<K, T, G>(PlanarUnfolder.PlanarUnfolding<K, T> unfolding, G geometryToTransform, int id)

            where T : IUnfoldablePlanarFace<K>
            where K : IUnfoldableEdge
            where G : Geometry
        {

            // grab all transforms that were applied to this surface id
            var map = unfolding.Maps;
            var applicableTransforms = map.Where(x => x.IDS.Contains(id)).ToList();


            // set the geometry to the first applicable transform
            //geometryToTransform = geometryToTransform.Transform(transforms.First()) as G;

            // get bb of geo to transform
            var myBox = BoundingBox.ByGeometry(geometryToTransform);
            // find the center of this box and use as start point
            var geoStartPoint = myBox.MinPoint.Add((myBox.MaxPoint.Subtract(myBox.MinPoint.AsVector()).AsVector().Scale(.5)));
            //create vector from unfold surface center startpoint and the current geo center and translate to this start position
            geometryToTransform = geometryToTransform.Translate(Vector.ByTwoPoints(geoStartPoint, unfolding.StartingPoints[id])) as G;

            // at this line, geo to transform is in the CS of the 
            //unfold surface and is that the same position, so following the transform
            // chain will bring the geo to a similar final location as the unfold

            return ApplyTransformations<G>(geometryToTransform, applicableTransforms);


        }

        /// <summary>
        /// overload that takes an explict startpoint to calculate the translation vector from
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="G"></typeparam>
        /// <param name="unfolding"></param>
        /// <param name="geometryToTransform"></param>
        /// <param name="id"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static G MapGeometryToUnfoldingByID<K, T, G>(PlanarUnfolder.PlanarUnfolding<K, T> unfolding, G geometryToTransform, int id, Point offset)

            where T : IUnfoldablePlanarFace<K>
            where K : IUnfoldableEdge
            where G : Geometry
        {

            // grab all transforms that were applied to this surface id
            var map = unfolding.Maps;
            var applicableTransforms = map.Where(x => x.IDS.Contains(id)).ToList();
            //var transforms = applicableTransforms.Select(x => x.CS).ToList();

            
            // set the geometry to the first applicable transform
            //geometryToTransform = geometryToTransform.Transform(transforms.First()) as G;

           // var geoStartPoint = offset;
            //create vector from unfold surface center startpoint and the current geo center and translate to this start position
           // geometryToTransform = geometryToTransform.Translate(Vector.ByTwoPoints(geoStartPoint, unfolding.StartingPoints[id])) as G;

            //TODO intermediate geometry needs to be cleaned up here

            // at this line, geo to transform is in the CS of the 
            //unfold surface and is that the same position, so following the transform
            // chain will bring the geo to a similar final location as the unfold

            return ApplyTransformations<G>(geometryToTransform, applicableTransforms);

        }
		# endregion
		
		//method that checks two lists of surfaces to see if one surface Object is in both,
		//this uses referenceEquals, so we're looking for the same object in memory
		private static bool referencesSameSurfaces(List<Surface> list1, List<Surface> list2)
        {

        foreach (var surf in list1 )
            {
            foreach (var surf2 in list2){
                if (ReferenceEquals(surf,surf2)){
                    return true;
                }
            
            }
            }
            return false;
        }


		//TODO?
        // I would like to expose PlanarunfoldingResult object with query methods
        // might be able to make the rest of these methods generic now....


        public static PlanarUnfolding<EdgeLikeEntity, FaceLikeEntity> Unfold(List<Face> faces)
        {
            var graph = ModelTopology.GenerateTopologyFromFaces(faces);

            //perform BFS on the graph and get back the tree
            var nodereturn = ModelGraph.BFS<EdgeLikeEntity, FaceLikeEntity>(graph);


            var casttree = nodereturn;

            return PlanarUnfold(casttree);

        }

        public static PlanarUnfolding<EdgeLikeEntity, FaceLikeEntity> Unfold(List<Surface> surfaces)
        {
            var graph = ModelTopology.GenerateTopologyFromSurfaces(surfaces);

            //perform BFS on the graph and get back the tree
            var nodereturn = ModelGraph.BFS<EdgeLikeEntity, FaceLikeEntity>(graph);


            var casttree = nodereturn;


            return PlanarUnfold(casttree);

        }

       
        /// <summary>
        /// method to avoid access exceptions
        /// in the case one occurs return a new surface
        /// this signifies that an intersection occured
        /// in the most current branch access exceptions
        /// have been avoided by removing polysurface creation, 
		/// still some geometry intersections cause this... Bugs have been filed
        /// </summary>
        /// <param name="surf1"></param>
        /// <param name="surf2"></param>
        /// <returns></returns>
        private static IEnumerable<object> SafeIntersect(this Surface surf1, Surface surf2)
        {
            try
            {
                var resultantGeo = surf1.Intersect(surf2);
                return resultantGeo;
            }
            catch (Exception e)
            {   
                Console.WriteLine(e.Message);
                //Geometry.ExportToSAT(new List<Geometry>{surf1,surf2},"C:\\Users\\Mike\\Desktop\\debugGeo");
                return new Geometry[1] { Surface.ByPatch(Rectangle.ByWidthLength()) };

            }

        }


        /// <summary>
        /// method that performs the main planar unfolding
        /// </summary>
        /// <param name="tree"></param>
        /// <returns></returns>
        public static PlanarUnfolding<K, T>
            PlanarUnfold<K, T>(List<GraphVertex<K, T>> tree)
            where K : IUnfoldableEdge
            where T : IUnfoldablePlanarFace<K>, new()
        {
            // this algorithm is a first test of recursive unfolding - overlapping is expected
            // but it dealth with by splitting branches

            //algorithm pseudocode follows:
            //Find the last ranked finishing time node in the BFS tree
            //Find the parent of this vertex
            //Fold them over their shared edge ( rotate child to be coplanar with parent)
            //merge the resulting faces into a set of surfaces
            // make this new surface the Face property in parent node
            //remove the child node we started with from the tree
            //repeat, until there is only one node in the tree.
            // at this point all faces should be coplanar with this surface

            //list of initial faces
            var allfaces = tree.Select(x => x.Face).ToList();
            //tree sorted by BFS finish time
            var sortedtree = tree.OrderBy(x => x.FinishTime).ToList();
            //set of unfold chunks that we break off from main unfold
            var disconnectedSet = new List<T>();
            //old geometry that we might need to cleanup at the end of unfold
            var oldGeometry = new List<List<Surface>>();

            List<FaceTransformMap> transforms = new List<FaceTransformMap>();


            // as an initial set, we'll record the identity matrix
            transforms.AddRange(allfaces.Select(x => new FaceTransformMap(CoordinateSystem.Identity(),CoordinateSystem.Identity(), x.IDS)).ToList());

			//keep folding until only one face remains
            while (sortedtree.Count > 1)
            {

                // if the tree only has nodes with no parents
                // then all branches have been folded into these
                //nodes and we should just return
                if (sortedtree.All(x => x.Parent == null))
                {
                    break;
                }

                // child is the highest finish time remaining in the list.
                var child = sortedtree.Last();
#if DEBUG
                Console.WriteLine("finish time of current child is " + child.FinishTime);
#endif
                var parent = child.Parent;
                //weak code, shoould have a method for this - find edge that leads to
                var edge = parent.GraphEdges.Where(x => x.Head.Equals(child)).First();
                if (edge == null)
                {
                    throw new Exception("edge was null something removed it from the graph");
                }


                // just check the initial faces against each other, these should only cotain single surfaces at this point
                double nc = AlignPlanarFaces.CheckNormalConsistency(child.Face, parent.Face, edge.GeometryEdge);
                //need to run this method on every surface contained in the UnfoldedSurfaceSet and collect them in a new list
                var rotationPackage = AlignPlanarFaces.GetCoplanarRotation(nc, child.UnfoldSurfaceSet, parent.Face, edge.GeometryEdge);
				List<Surface> rotatedFace = rotationPackage.Item1;
                //TODO do I cleanup this rotation package anywhere? the plane from it specifically?

                //at this point need to check if the rotated face has intersected with any other face that has been been
                // folded already, all of these already folded faces should exist either in the parent unfoldedSurfaceSet
                // or they should have been moved away, we should only need to check if the rotated face hits the unfoldedSurfaceSet.

                // the srflist is either a single surface or all surfaces containeed in the set, if any of these
                // surfaces intersected with the rotatedface returns a new surface then there is a real overlap and 
                // we need to move the rotated face away. it will need to be oriented horizontal later


                // perfrom the intersection test, from surfaces in the parent unfoldset against all surfaces in rotatedFaceset

                // this linq statement was not releasing memory until the very end
                // so all intersection results were kept in memory
                //with 2000 surfaces this was 1 gig of memory
                //  bool overlapflag = parent.UnfoldSurfaceSet.SurfaceEntities.SelectMany(a => rotatedFace.SelectMany(a.Intersect)).OfType<Surface>().Any();

                var overlapflag = false;
                foreach (var surf1 in parent.UnfoldSurfaceSet.SurfaceEntities)
                {
                    foreach (var surf2 in rotatedFace)
                    {
                        var resultGeo = surf1.SafeIntersect(surf2);
                        if (resultGeo.OfType<Surface>().Any())
                        {
                            overlapflag = true;
                            //dispose all the intersection geometry
                            foreach (IDisposable item in resultGeo)
                            {
                                item.Dispose();
                            }
                            goto exitloops;
                            // thats right, goto!
                        }
                        //dispose all intersection geometry
                        foreach (IDisposable item in resultGeo)
                        {
                            item.Dispose();
                        }

                    }
                }


            exitloops:

                if (overlapflag)
                {


                    // if any intersection result was a surface then we overlapped, we need to pick a new
                    // branch to start the unfold from

					//add this unfolded branch to a list of disconnected branches and push the child initial ID into the 
					//set of IDS we folded
                    disconnectedSet.Add(child.UnfoldSurfaceSet);
                    child.UnfoldSurfaceSet.IDS.Add(child.Face.ID);
					
                }

                else
                {
                    // if there is no overlap we need to merge the rotated chain into the parent
                    // add the parent surfaces into this list of surfaces we'll use to create a new facelike
                    List<Surface> subsurblist = new List<Surface>(parent.UnfoldSurfaceSet.SurfaceEntities);
                    // then push the rotatedFace surfaces so these are after the parent's surfaces... 
                    // this order is important since all the algorithms use the first surface in the list
                    // for calculations of rotation etc.... needs to be hardened and either moved all out of
                    // classes and into algorithm or vice versa...
                    subsurblist.AddRange(rotatedFace);


                    // idea is to push the rotatedface - which might be a list of surfaces or surface into
                    // the parent vertex's unfoldSurfaceSet property, then to contract the graph, removing the child node.
                    // at the same time we are trying to build a map of all the rotation transformations we are producing
                    // and to which faces they have been applied, we must push the intermediate coordinate systems
                    // as well as the ids to which they apply through the graph as well.

                    // need to extract the parentIDchain, this is previous faces that been made coplanar with the parent
                    // we need to grab them before the parent unfoldchain is replaced
                    var parentIDchain = parent.UnfoldSurfaceSet.IDS;

                    // replace the surface in the parent with the wrapped chain of surfaces
                    var wrappedChainOfUnfolds = new T();
                    wrappedChainOfUnfolds.SurfaceEntities = subsurblist;
                    wrappedChainOfUnfolds.OriginalEntity = wrappedChainOfUnfolds.SurfaceEntities;

                    parent.UnfoldSurfaceSet = wrappedChainOfUnfolds;

                    // as we rotate up the chain we'll add the new IDs entry to the list on the parent.

                    var rotatedFaceIDs = child.UnfoldSurfaceSet.IDS;
                    // add the child ids to the parent id list
                    parent.UnfoldSurfaceSet.IDS.AddRange(rotatedFaceIDs);
                    parent.UnfoldSurfaceSet.IDS.Add(child.Face.ID);

                    // note that we add the parent ID chain to the parent unfold chain, replacing it
                    // but that we DO NOT add these ids to the current transformation map, since we're not transforming them
                    // right now, we just need to keep them from being deleted while adding the new ids.

                    parent.UnfoldSurfaceSet.IDS.AddRange(parentIDchain);
                    // now add the coordinate system for the rotatedface to the transforms list

                    var currentIDsToStoreTransforms = new List<int>();

                    currentIDsToStoreTransforms.Add(child.Face.ID);
                    currentIDsToStoreTransforms.AddRange(rotatedFaceIDs);
                    //here we create a new transformation map that represents the rotation we just perfromed to all these child surfaces
                    transforms.Add(new FaceTransformMap(
                        rotationPackage.Item2,rotationPackage.Item3, currentIDsToStoreTransforms));


                }
                // shrink the tree
                child.RemoveFromGraph(sortedtree);
                //child.RemoveFromGraph(tree);

                // if the rotated faces we just generated are not being folded into the root
                // of the tree then we should add them to the list to dispose
                if (parent.FinishTime != 0 && rotatedFace != null)
                {

                    oldGeometry.Add(rotatedFace);
                }
            }
            // at this point we may have a main trunk with y nodes in it, and x disconnected branches
			//we'll collect all the different branches to return the geometry

            // collect all surface lists
            var masterFacelikeSet = sortedtree.Select(x => x.UnfoldSurfaceSet).ToList();
            masterFacelikeSet.AddRange(disconnectedSet);

            
            // merge the main trunk and the disconnected sets
			//this is a hacky way of creating a copy (translation by 0)
            var maintree = sortedtree.Select(x => x.UnfoldSurfaceSet.SurfaceEntities.Select(y=>y.Translate(0,0,0) as Surface).ToList()).ToList();
            maintree.AddRange(disconnectedSet.Select(x => x.SurfaceEntities.Select(y=>y.Translate(0,0,0) as Surface).ToList()).ToList());

            //iterate each list of rotated surfaces in the old geo list
            foreach (var oldgeolist in oldGeometry)
            { //against each set of surfaces in the disconnected sets
                var dispose = true;
                foreach (var set in disconnectedSet)
                {   // if they are equal to any set in the disconnected set,
                    // then set a flag and breakout
                    if (referencesSameSurfaces(oldgeolist, set.SurfaceEntities))
                    {
                        dispose = false;
                        break;
                    }
                }
                if (dispose)
                {
                    foreach (IDisposable item in oldgeolist)
                    {
                        item.Dispose();
                    }
                }
            }

            // return a planarUnfolding that represents this unfolding
            return new PlanarUnfolding<K, T>(allfaces, maintree, transforms, masterFacelikeSet,tree);
        }

    }
}
