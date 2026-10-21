extends RefCounted
## Main owns one lifecycle and interaction state shared by host components.

var started := false
var opening := false
var opening_failed := false
var draining := false
var closing := false
var shutdown_finished := false
var reading := false
var player_options_open := false
var background_execution := false
var completion_pending := false
var book_reading := false
var bookmark_loading := false
var bookmark_saving := false
var bookmark_requested := false
var view_faulted := false
