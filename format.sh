#!/bin/bash
astyle --options=astylerc --formatted --recursive "*.cs"

#CSS formatting commands
for i in `find . -iname "*.css"`; do cssbeautify-cli -i2 -f $i > $i.new ; mv $i.new $i; done
