<!DOCTYPE html>
<html>
	<head>
		<meta charset="utf-8" />
		<meta http-equiv="X-UA-Compatible" content="IE=edge" />
		<meta name="viewport" content="width=device-width, initial-scale=1.0" />
		<meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
		
		<title>[#SITE_TITLE#]</title>
		
		<link rel="stylesheet" type="text/css" href="styles/default/layout.css" />
		<link rel="stylesheet" type="text/css" href="styles/default/style.css" />
		
		<script type="text/javascript" src="scripts/jquery.js"></script>
		<script type="text/javascript" src="scripts/functions.js"></script>
		<!--[if lt IE 9]>
		<script type="text/javascript">
			document.createElement('header');
			document.createElement('nav');
			document.createElement('aside');
			document.createElement('section');
			document.createElement('main');
		</script>
		<![endif]-->
	</head>
	<body>
		<div id="page">
			<header>
				[[site-header]]
			</header>

			<nav>
				[[site-navigation]]
			</nav>
		
			<main id="content">
				[[site-content]]
			</main>
		
			<aside>
				[[site-aside]]
			</aside>
		
			<footer>
				[[site-footer]]
			</footer>
		</div>
	</body>
</html>
