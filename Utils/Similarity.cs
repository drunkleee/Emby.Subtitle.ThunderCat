using System;
using System.Collections.Generic;
using System.Linq;

namespace Emby.Subtitle.OneOneFiveMaster.Utils
{
    public static class Similarity
    {
        /// <summary>
        /// Calculates the Jaccard similarity coefficient between two strings.
        /// </summary>
        public static double GetJaccardSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0.0;

            var sourceSet = new HashSet<char>(source.ToLowerInvariant());
            var targetSet = new HashSet<char>(target.ToLowerInvariant());

            var intersection = sourceSet.Intersect(targetSet).Count();
            var union = sourceSet.Union(targetSet).Count();

            return union == 0 ? 0.0 : (double)intersection / union;
        }

        // Maybe token-based is better for filenames?
        // 115master might use specialized logic.
    }
}
