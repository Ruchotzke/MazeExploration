using Delaunay.Geometry;
using GeneralUnityUtils.PriorityQueue;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Delaunay.Voronoi
{
    public class Voronoi : MonoBehaviour
    {

        /// <summary>
        /// Generate a new voronoi diagram for the given inputs.
        /// </summary>
        /// <param name="bounds">The boundary rectangle of the voronoi diagram.</param>
        /// <param name="sites"></param>
        public Voronoi(Rect bounds, List<float2> sites)
        {

        }

        /// <summary>
        /// Compute the voronoi diagram for a given set of input sites.
        /// This uses fortunes algorithm, which is a time complexity improvement over
        /// Bowyer-Watson + Dual Graph.
        /// </summary>
        /// <param name="sites">The list of sites to be converted into a voronoi diagram.</param>
        /// <returns></returns>
        public static List<(float2 site, Polygon polygon)> CalculateVoronoi(List<float2> sites)
        {
            /* Set up the event queue */
            PriorityQueue<FortuneEvent> Queue = new PriorityQueue<FortuneEvent>();

            /* Add a site event into the queue for each incoming site */
            foreach(var site in sites)
            {
                Queue.Enqueue(new FortuneEvent(true, site), -site.y); //we want to use -site.y since the sweepline moves from +y to -y
            }

            /* Continually remove the next item from the queue */
            while(Queue.Size > 0)
            {
                /* Grab the next item */
                var next = Queue.Dequeue();

                /* If this is a site event, add a new site. Otherwise, handle it as a circle event */
                if (next.SiteEvent)
                {

                }
                else
                {

                }
            }

            return null;
        }

        /// <summary>
        /// Helper - add a new site into the beachline.
        /// </summary>
        /// <param name="u"></param>
        private static void AddParabola(float2 u)
        {

        }


    }
}

