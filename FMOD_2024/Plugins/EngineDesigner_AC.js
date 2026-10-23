studio.menu.addMenuItem({
	name: "Engine Designer for AssettoCorsa",
	keySequence: "Ctrl+Shift+E",
	execute: function() {
		
		var revLimit = 8000;
		var revIdle = 1000;
		var engResponse = 30;
		var engLoad = 5;
		var throttleSmooth = 50;
		
		studio.ui.showModelessDialog({
			windowTitle: "Engine Designer for AssettoCorsa",
			windowWidth: 250,
			windowHeight: 360,
			widgetType: studio.ui.widgetType.Layout,
			layout: studio.ui.layoutType.VBoxLayout,
			sizePolicy: studio.ui.sizePolicy.MinimumExpanding,
			spacing: 12,
			items: [
				{ widgetType: studio.ui.widgetType.Label, widgetId: "m_logo", text: "<img src=\"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAKQAAAAZCAYAAAC/4YXqAAAAGXRFWHRTb2Z0d2FyZQBBZG9iZSBJbWFnZVJlYWR5ccllPAAAAyJpVFh0WE1MOmNvbS5hZG9iZS54bXAAAAAAADw/eHBhY2tldCBiZWdpbj0i77u/IiBpZD0iVzVNME1wQ2VoaUh6cmVTek5UY3prYzlkIj8+IDx4OnhtcG1ldGEgeG1sbnM6eD0iYWRvYmU6bnM6bWV0YS8iIHg6eG1wdGs9IkFkb2JlIFhNUCBDb3JlIDUuMy1jMDExIDY2LjE0NTY2MSwgMjAxMi8wMi8wNi0xNDo1NjoyNyAgICAgICAgIj4gPHJkZjpSREYgeG1sbnM6cmRmPSJodHRwOi8vd3d3LnczLm9yZy8xOTk5LzAyLzIyLXJkZi1zeW50YXgtbnMjIj4gPHJkZjpEZXNjcmlwdGlvbiByZGY6YWJvdXQ9IiIgeG1sbnM6eG1wPSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvIiB4bWxuczp4bXBNTT0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wL21tLyIgeG1sbnM6c3RSZWY9Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC9zVHlwZS9SZXNvdXJjZVJlZiMiIHhtcDpDcmVhdG9yVG9vbD0iQWRvYmUgUGhvdG9zaG9wIENTNiAoV2luZG93cykiIHhtcE1NOkluc3RhbmNlSUQ9InhtcC5paWQ6N0UyMUQ1QzZERkMxMTFFNzlCNUZFNkZEQ0M0NzlERUUiIHhtcE1NOkRvY3VtZW50SUQ9InhtcC5kaWQ6N0UyMUQ1QzdERkMxMTFFNzlCNUZFNkZEQ0M0NzlERUUiPiA8eG1wTU06RGVyaXZlZEZyb20gc3RSZWY6aW5zdGFuY2VJRD0ieG1wLmlpZDo3RTIxRDVDNERGQzExMUU3OUI1RkU2RkRDQzQ3OURFRSIgc3RSZWY6ZG9jdW1lbnRJRD0ieG1wLmRpZDo3RTIxRDVDNURGQzExMUU3OUI1RkU2RkRDQzQ3OURFRSIvPiA8L3JkZjpEZXNjcmlwdGlvbj4gPC9yZGY6UkRGPiA8L3g6eG1wbWV0YT4gPD94cGFja2V0IGVuZD0iciI/PhEg35wAAAbKSURBVHja7FtdbFRF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\">", alignment: studio.ui.alignment.AlignHCenter, sizePolicy: studio.ui.sizePolicy.Maximum },
				{
					widgetType: studio.ui.widgetType.Layout,
					widgetId: "m_controls",
					layout: studio.ui.layoutType.HBoxLayout,
					contentsMargins: { left: 0, top: 0, right: 0, bottom: 0 },
					spacing: 20,
					items: [
						{
							widgetType: studio.ui.widgetType.Layout,
							layout: studio.ui.layoutType.VBoxLayout,
							contentsMargins: { left: 0, top: 0, right: 0, bottom: 0 },
							stretchFactor: 1,
							items: [
								{ widgetType: studio.ui.widgetType.Label, widgetId: "m_rpm", sizePolicy: studio.ui.sizePolicy.Maximum },
								{ widgetType: studio.ui.widgetType.Spacer, stretchFactor: 0.1 },
								{ widgetType: studio.ui.widgetType.Label, text: "Throttle", alignment: studio.ui.alignment.AlignHCenter },
								{
									row: 1, column: 0,
									widgetType: studio.ui.widgetType.Slider,
									widgetId: "m_throttle",
									range: { minimum: 0, maximum: 10000 },
									value: 0,
									orientation: studio.ui.orientation.Vertical,
									alignment: studio.ui.alignment.AlignHCenter,
								},
							],
						},
						{
							widgetType: studio.ui.widgetType.Layout,
							layout: studio.ui.layoutType.VBoxLayout,
							contentsMargins: { left: 0, top: 0, right: 0, bottom: 0 },
							spacing: 6,
							sizePolicy: { horizontalPolicy: studio.ui.sizePolicy.Fixed },
							items: [
								{ widgetType: studio.ui.widgetType.Label, text: "Rev Limit" },
								{
									widgetType: studio.ui.widgetType.SpinBox,
									widgetId: "m_revLimit",
									range: { minimum: 0, maximum: 20000 },
									value: revLimit,
									orientation: studio.ui.orientation.Horizontal,
								},
								{ widgetType: studio.ui.widgetType.Spacer, stretchFactor: 0.15 },
								{ widgetType: studio.ui.widgetType.Label, text: "Rev Idle" },
								{
									widgetType: studio.ui.widgetType.SpinBox,
									widgetId: "m_revIdle",
									range: { minimum: 0, maximum: 20000 },
									value: revIdle,
									orientation: studio.ui.orientation.Horizontal,
								},
								{ widgetType: studio.ui.widgetType.Spacer, stretchFactor: 0.15 },
								{ widgetType: studio.ui.widgetType.Label, text: "Response Followability" },
								{
									widgetType: studio.ui.widgetType.SpinBox,
									widgetId: "m_engineResponse_spin",
									range: { minimum: 0, maximum: 100 },
									value: engResponse,
									orientation: studio.ui.orientation.Horizontal,
									onValueChanged: function() { this.findWidget("m_engineResponse_slid").setValue(this.value()) }
								},
								{
									widgetType: studio.ui.widgetType.Slider,
									widgetId: "m_engineResponse_slid",
									range: { minimum: 0, maximum: 100 },
									value: engResponse,
									orientation: studio.ui.orientation.Horizontal,
									onValueChanged: function() { this.findWidget("m_engineResponse_spin").setValue(this.value()) }
								},
								{ widgetType: studio.ui.widgetType.Spacer, stretchFactor: 0.15 },
								{ widgetType: studio.ui.widgetType.Label, text: "Engine Load" },
								{
									widgetType: studio.ui.widgetType.SpinBox,
									widgetId: "m_engineLoad_spin",
									range: { minimum: 0, maximum: 100 },
									value: engLoad,
									orientation: studio.ui.orientation.Horizontal,
									onValueChanged: function() { this.findWidget("m_engineLoad_slid").setValue(this.value()) }
								},
								{
									widgetType: studio.ui.widgetType.Slider,
									widgetId: "m_engineLoad_slid",
									range: { minimum: 0, maximum: 100 },
									value: engLoad,
									orientation: studio.ui.orientation.Horizontal,
									onValueChanged: function() { this.findWidget("m_engineLoad_spin").setValue(this.value()) }
								},
								{ widgetType: studio.ui.widgetType.Spacer, stretchFactor: 0.15 },
								{ widgetType: studio.ui.widgetType.Label, text: "Throttle Smooth" },
								{
									widgetType: studio.ui.widgetType.SpinBox,
									widgetId: "m_throttleSmooth_spin",
									range: { minimum: 0, maximum: 100 },
									value: throttleSmooth,
									orientation: studio.ui.orientation.Horizontal,
									onValueChanged: function() { this.findWidget("m_throttleSmooth_slid").setValue(this.value()) }
								},
								{
									widgetType: studio.ui.widgetType.Slider,
									widgetId: "m_throttleSmooth_slid",
									range: { minimum: 0, maximum: 100 },
									value: throttleSmooth,
									orientation: studio.ui.orientation.Horizontal,
									onValueChanged: function() { this.findWidget("m_throttleSmooth_spin").setValue(this.value()) }
								},
								{ widgetType: studio.ui.widgetType.Spacer, stretchFactor: 1 },
							],
						}
					],
				},
				{ widgetType: studio.ui.widgetType.Spacer, stretchFactor: 1 },
				{ widgetType: studio.ui.widgetType.Label, widgetId: "m_logo", text: "<img src=\"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAALwAAAAuCAYAAACS53PjAAAAGXRFWHRTb2Z0d2FyZQBBZG9iZSBJbWFnZVJlYWR5ccllPAAAAyJpVFh0WE1MOmNvbS5hZG9iZS54bXAAAAAAADw/eHBhY2tldCBiZWdpbj0i77u/IiBpZD0iVzVNME1wQ2VoaUh6cmVTek5UY3prYzlkIj8+IDx4OnhtcG1ldGEgeG1sbnM6eD0iYWRvYmU6bnM6bWV0YS8iIHg6eG1wdGs9IkFkb2JlIFhNUCBDb3JlIDUuMy1jMDExIDY2LjE0NTY2MSwgMjAxMi8wMi8wNi0xNDo1NjoyNyAgICAgICAgIj4gPHJkZjpSREYgeG1sbnM6cmRmPSJodHRwOi8vd3d3LnczLm9yZy8xOTk5LzAyLzIyLXJkZi1zeW50YXgtbnMjIj4gPHJkZjpEZXNjcmlwdGlvbiByZGY6YWJvdXQ9IiIgeG1sbnM6eG1wPSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvIiB4bWxuczp4bXBNTT0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wL21tLyIgeG1sbnM6c3RSZWY9Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC9zVHlwZS9SZXNvdXJjZVJlZiMiIHhtcDpDcmVhdG9yVG9vbD0iQWRvYmUgUGhvdG9zaG9wIENTNiAoV2luZG93cykiIHhtcE1NOkluc3RhbmNlSUQ9InhtcC5paWQ6NjlGQUUwNzNERkMxMTFFNzk5ODRFRjQyRjU3RjdBNDMiIHhtcE1NOkRvY3VtZW50SUQ9InhtcC5kaWQ6NjlGQUUwNzRERkMxMTFFNzk5ODRFRjQyRjU3RjdBNDMiPiA8eG1wTU06RGVyaXZlZEZyb20gc3RSZWY6aW5zdGFuY2VJRD0ieG1wLmlpZDo2OUZBRTA3MURGQzExMUU3OTk4NEVGNDJGNTdGN0E0MyIgc3RSZWY6ZG9jdW1lbnRJRD0ieG1wLmRpZDo2OUZBRTA3MkRGQzExMUU3OTk4NEVGNDJGNTdGN0E0MyIvPiA8L3JkZjpEZXNjcmlwdGlvbj4gPC9yZGY6UkRGPiA8L3g6eG1wbWV0YT4gPD94cGFja2V0IGVuZD0iciI/PpiWSckAABYZSURBVHja7F0JeFPF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\">", alignment: studio.ui.alignment.AlignHCenter, sizePolicy: studio.ui.sizePolicy.Maximum },
			],
			onConstructed: function() {
				this.smoothedRPM = 0;
				this.smoothedLoad = 0;
				this.timerId = this.startTimer(20);
			},
			onTimerEvent: function(timerId) {
				if(timerId !== this.timerId)
					return;
				
				var event = studio.window.browserCurrent();
				var rpmParameter = null;
				var loadParameter = null;
				
				if(event && event.isOfType("Event")) {	
					event.parameters.forEach(function(parameter) {
						if(new RegExp('rpms', 'gi').test(parameter.name)) {
							rpmParameter = parameter;
						}
						else if(new RegExp('throttle', 'gi').test(parameter.name)) {
							loadParameter = parameter;
						}
					});
				}
				
				if(rpmParameter && loadParameter) {
					this.findWidget("m_controls").setEnabled(true);
					
					revLimit = this.findWidget("m_revLimit").value()
					revIdle = this.findWidget("m_revIdle").value()
					
					var engRev = rpmParameter.cursorPosition;
					var targetLoad = (this.findWidget("m_throttle").value() / 10000);
					
					throttleSmooth = 1 - (this.findWidget("m_throttleSmooth_slid").value() / 100);
					engResponse = (this.findWidget("m_engineResponse_slid").value() / 1000);
					engLoad = 1 + (this.findWidget("m_engineLoad_slid").value() / 3);
					
					if(engRev < revIdle && targetLoad < 10000) {
						targetLoad += (revIdle - engRev) * 0.01;
					}
					if(engRev >= revLimit) {
						this.smoothedLoad = targetLoad - 1;
					} else {
					   	this.smoothedLoad += (targetLoad - this.smoothedLoad) * throttleSmooth;
					}
					loadParameter.cursorPosition = loadParameter.minimum + (this.smoothedLoad * (loadParameter.maximum - loadParameter.minimum));
					
					var smoothedRPM_ = (this.smoothedLoad - this.smoothedRPM) * engResponse;
					this.smoothedRPM += smoothedRPM_ < 0 ? smoothedRPM_ : (smoothedRPM_ / engLoad);
					var rpm = rpmParameter.minimum + (this.smoothedRPM * (rpmParameter.maximum - rpmParameter.minimum));
					if(rpm > revLimit) rpm = revLimit;
					rpmParameter.cursorPosition = rpm;
					
					this.findWidget("m_rpm").setText('<div style=\"display:block;background-color:#333;color:#fff;line-height:24px;font-size:18px;\"><p align=\"right\">'+ Math.floor(rpm) +'<span style=\"font-size:12px;color:#aaa\">rpm<span>&nbsp;</p></div>');
				}
				else {
					this.findWidget("m_rpm").setText('<div style=\"display:block;background-color:#333;color:#aaa;line-height:24px;font-size:18px;\"><p align=\"right\">&nbsp;<span style=\"font-size:12px;\">rpm<span>&nbsp;</p></div>');
					this.findWidget("m_controls").setEnabled(false);
				}
			}
		});
	},
});
