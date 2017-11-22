function LoadDocument(url, target, title, action, data)
{
	var u = url;
	if (data != '')
	{
		u = url + "?" + data;
	}
	
	$.ajax(
	{
		url: u,
		method: "GET",
		cache: false,
		beforeSend: function(request)
		{
			request.setRequestHeader("Request-Type", "async");
			request.setRequestHeader("UAgent", "Netvision");
			if (action != '')
			{
				request.setRequestHeader("Action", action);
			}
			
			$(target).fadeOut("1300", function()
			{
				$(target).html('');
			});
		}
	})
	.done(function(html)
	{
		$(target).fadeIn("1300", function()
		{
			$(target).html(html);
		});
	})
	.fail(function (html)
	{
		$(target).fadeIn("1300", function()
		{
			$(target).html(html);
		});
	});
}

function sendForm(url, target, sender, form, action)
{
	$(form).submit(function()
	{
		$.ajax(
		{
			method: "POST",
			url: url,
			data: $(form).serialize(),
			beforeSend: function(xhr)
			{
				xhr.setRequestHeader("Request-Type", "async");
				xhr.setRequestHeader("UAgent", "Netvision");

				if (action != '')
				{
					xhr.setRequestHeader("Action", action);
				}

				$(sender).fadeOut("1300");
				$(sender).html('');
			}
		})
		.done(function(data)
		{
			$(target).fadeIn("1300", function()
			{
				$(target).html(data);
			});
		})
		.fail(function(data)
		{
			$(target).fadeIn("1300", function()
			{
				$(target).html(data);
			});
		});
	});
}

function toggle(o, n)
{
	$(o).fadeOut("50", function()
	{
		$(n).fadeIn("50");
	});
}

function Window(url, title)
{
	window.open(url, "win", title, "toolbar=no,location=0,height=100,width=200");
}
