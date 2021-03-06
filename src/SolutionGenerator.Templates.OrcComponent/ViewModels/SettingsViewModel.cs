﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SettingsViewModel.cs" company="WildGums">
//   Copyright (c) 2012 - 2016 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SolutionGenerator.Templates.OrcComponent.ViewModels
{
    using Catel;
    using Catel.MVVM;

    public class SettingsViewModel : ViewModelBase
    {
        private readonly OrcComponentTemplateContext _templateContext;

        public SettingsViewModel(OrcComponentTemplateContext templateContext)
        {
            Argument.IsNotNull(() => templateContext);

            _templateContext = templateContext;

            NuGet = templateContext.NuGet;
            GitHub = templateContext.GitHub;
        }

        public NuGetTemplate NuGet { get; private set; }

        public GitHubTemplate GitHub { get; private set; }
    }
}