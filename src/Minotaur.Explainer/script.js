function _toggle(id) {
	var div = document.getElementById(id);
	var btn = div.children[0];
	var body = div.children[1];

	if (body.style.display === "none") {
		body.style.display = "block";
		btn.innerHTML = "[-]";
	} else {
		body.style.display = "none";
		btn.innerHTML = "[+]";
	}
}
