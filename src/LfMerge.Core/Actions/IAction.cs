﻿// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge.Core.Actions
{
	public interface IAction
	{
		ActionNames Name { get; }

		IAction NextAction { get; }

		void Run(ILfProject project);
	}
}

