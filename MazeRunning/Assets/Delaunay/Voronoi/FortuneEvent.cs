using Delaunay.Geometry;
using GeneralUnityUtils.PriorityQueue;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Delaunay.Voronoi
{
    /// <summary>
    /// <summary>
    /// An event class used to facilitate fortune's algorithm.
    /// </summary>
    public class FortuneEvent
    {
        public float2 site;
        public bool SiteEvent;

        public FortuneEvent(bool siteEvent, float2 site)
        {
            this.site = site;
            SiteEvent = siteEvent;
        }
    }
}

