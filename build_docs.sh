#!/usr/bin/env bash
rm -rf docs/api_reference
doxygen Doxyfile
rm -rf docs/presentation
cd presentation-source
npm run build
cd -

