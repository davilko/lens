﻿using System;
using Lens.SyntaxTree;

namespace Lens
{
	/// <summary>
	/// A generic exception that has occured during parse.
	/// </summary>
	public class LensCompilerException : Exception
	{
		public LensCompilerException(string msg) : base(msg)
		{ }

		public LensCompilerException(string msg, LocationEntity entity) : base(msg)
		{
			BindToLocation(entity);
		}

		/// <summary>
		/// Start of the erroneous segment.
		/// </summary>
		public LexemLocation StartLocation { get; private set; }

		/// <summary>
		/// End of the erroneous segment.
		/// </summary>
		public LexemLocation EndLocation { get; private set; }

		/// <summary>
		/// Full message with error positions.
		/// </summary>
		public string FullMessage
		{
			get
			{
				if (StartLocation.Line == 0 && StartLocation.Offset == 0 && EndLocation.Line == 0 && EndLocation.Offset == 0)
					return Message;

				return string.Format(
					"{0}\nLocation: {1}:{2} ... {3}:{4}",
					Message,
					StartLocation.Line,
					StartLocation.Offset,
					EndLocation.Line,
					EndLocation.Offset
				);
			}
		}

		/// <summary>
		/// Bind exception to a location.
		/// </summary>
		/// <param name="entity"></param>
		public void BindToLocation(LocationEntity entity)
		{
			if (entity == null)
				return;

			StartLocation = entity.StartLocation;
			EndLocation = entity.EndLocation;
		}
	}
}