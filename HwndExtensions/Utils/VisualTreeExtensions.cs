using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace HwndExtensions.Utils
{
    public static class VisualTreeExtensions
    {
        /// <summary>
        /// Finds a parent of a given item on the visual tree that is of type T. 
        /// And the optional predicate returns true for the element
        /// </summary>
        /// <returns>The matching UIElement, or null if it could not be found.</returns>
        public static T TryFindVisualAncestor<T>(DependencyObject depObj, Predicate<T> predicate = null) where T : class
        {
            DependencyObject current = depObj;
            while (current != null)
            {
                T match = current as T;
                if (match != null && (predicate == null || predicate(match)))
                {
                    return match;
                }
                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        /// <summary>
        /// Tries to find the top parent - The top-most parent of this type in the visual tree.
        /// </summary>
        /// <typeparam name="T">The type of the parent</typeparam>
        /// <param name="depObj">The hit test result.</param>
        /// <returns>The matching UIElement, or null if it could not be found.</returns>
        public static T TryFindTopVisualAncestor<T>(DependencyObject depObj) where T : class
        {
            T current = null;
            T aboveCurrent = TryFindVisualAncestor<T>(depObj);

            while (aboveCurrent != null)
            {
                current = aboveCurrent;
                aboveCurrent = TryFindVisualAncestor<T>(current as DependencyObject);
            }

            return current;
        }

        /// <summary>
        /// Find all visual descendents of type T
        /// </summary>
        /// <param name="root"></param>
        /// <param name="searchWithinAFoundT">search within T's descendents for more T's</param>
        public static List<T> FindVisualChildren<T>(DependencyObject root, bool searchWithinAFoundT = true) where T : DependencyObject
        {
            List<T> list = new List<T>();
            if (root != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(root, i);
                    if (child is T)
                    {
                        list.Add((T)child);

                        // this means that an element is not expected to contain elements of his type
                        if (!searchWithinAFoundT) { continue; }
                    }

                    List<T> childItems = FindVisualChildren<T>(child, searchWithinAFoundT);
                    if (childItems != null && childItems.Count > 0)
                    {
                        foreach (var item in childItems)
                        {
                            list.Add(item);
                        }
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Performs a DFS search for finding the first visual child of type T
        /// </summary>
        /// <typeparam name="T">The type to search for</typeparam>
        /// <param name="root">The root to search under</param>
        /// <returns>The found child</returns>
        public static T FindFirstChild<T>(DependencyObject root) where T : class
        {
            if (root != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(root, i);
                    T childT = child as T;
                    if (childT != null) return childT;

                    T descendantT = FindFirstChild<T>(child);
                    if (descendantT != null) return descendantT;
                }
            }
            return null;
        }
    }
}
